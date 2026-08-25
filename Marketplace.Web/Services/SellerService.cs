using Marketplace.Web.Data;
using Marketplace.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Services;

public class SellerService : ISellerService
{
    private readonly AppDbContext _db;
    private readonly IPaymentGateway _paymentGateway;

    /// A payout below this isn't worth a bank transfer.
    public const decimal MinimumPayout = 10m;

    // The platform's cut. A single constant, referenced by both the earnings
    // page and the order-level breakdown, so the seller can never be shown
    // two different net figures for the same order.
    public const decimal PlatformFeeRate = 0.10m;

    public static decimal FeeOn(decimal gross) => Math.Round(gross * PlatformFeeRate, 2, MidpointRounding.AwayFromZero);
    public static decimal NetOf(decimal gross) => gross - FeeOn(gross);

    public SellerService(AppDbContext db, IPaymentGateway paymentGateway)
    {
        _db = db;
        _paymentGateway = paymentGateway;
    }

    public async Task<SellerProfile?> GetProfileAsync(int userId) =>
        await _db.SellerProfiles
            .Include(sp => sp.PickupLocations)
            .FirstOrDefaultAsync(sp => sp.UserId == userId);

    public async Task<SellerProfile> CreateProfileAsync(int userId, SellerOnboardingRequest request)
    {
        var existing = await _db.SellerProfiles.FirstOrDefaultAsync(sp => sp.UserId == userId);
        if (existing is not null) return existing; // idempotent — a double submit shouldn't create a second kitchen

        var profile = new SellerProfile
        {
            UserId = userId,
            Suburb = request.Suburb.Trim(),
            Cuisine = request.Cuisine.Trim(),
            Story = request.Story.Trim(),
            // A brand-new cook is Pending, not Verified: this app tracks where
            // someone is in council/state food-business registration, it never
            // asserts they've completed it (see VerificationStatus).
            VerificationStatus = VerificationStatus.Pending,
            JoinedAt = DateTime.UtcNow,
            RatingAverage = null, // reads as "New cook" until real reviews land
            CompletedOrders = 0,
            RepeatCustomers = 0,
        };
        _db.SellerProfiles.Add(profile);
        await _db.SaveChangesAsync();

        _db.PickupLocations.Add(new PickupLocation
        {
            SellerProfileId = profile.Id,
            Label = string.IsNullOrWhiteSpace(request.LocationLabel) ? "Front door" : request.LocationLabel.Trim(),
            Suburb = request.Suburb.Trim(),
            ExactAddress = request.ExactAddress.Trim(),
            Instructions = request.Instructions.Trim(),
            ApproxDistanceKm = request.ApproxDistanceKm <= 0 ? 1.0 : request.ApproxDistanceKm,
        });
        await _db.SaveChangesAsync();

        return profile;
    }

    public async Task<bool> UpdateProfileAsync(int userId, string suburb, string cuisine, string story)
    {
        var profile = await _db.SellerProfiles.FirstOrDefaultAsync(sp => sp.UserId == userId);
        if (profile is null) return false;
        profile.Suburb = suburb.Trim();
        profile.Cuisine = cuisine.Trim();
        profile.Story = story.Trim();
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<PickupLocation>> GetPickupLocationsAsync(int userId)
    {
        var profile = await _db.SellerProfiles.AsNoTracking().FirstOrDefaultAsync(sp => sp.UserId == userId);
        if (profile is null) return new();
        return await _db.PickupLocations.AsNoTracking()
            .Where(l => l.SellerProfileId == profile.Id)
            .OrderBy(l => l.Id)
            .ToListAsync();
    }

    public async Task<PickupLocation?> AddPickupLocationAsync(
        int userId, string label, string suburb, string exactAddress, string instructions, double approxDistanceKm)
    {
        var profile = await _db.SellerProfiles.AsNoTracking().FirstOrDefaultAsync(sp => sp.UserId == userId);
        if (profile is null) return null;

        var location = new PickupLocation
        {
            SellerProfileId = profile.Id,
            Label = label.Trim(),
            Suburb = suburb.Trim(),
            ExactAddress = exactAddress.Trim(),
            Instructions = instructions.Trim(),
            ApproxDistanceKm = approxDistanceKm <= 0 ? 1.0 : approxDistanceKm,
        };
        _db.PickupLocations.Add(location);
        await _db.SaveChangesAsync();
        return location;
    }

    public async Task<bool> DeletePickupLocationAsync(int userId, int locationId)
    {
        var profile = await _db.SellerProfiles.AsNoTracking().FirstOrDefaultAsync(sp => sp.UserId == userId);
        if (profile is null) return false;

        var location = await _db.PickupLocations.FirstOrDefaultAsync(l => l.Id == locationId && l.SellerProfileId == profile.Id);
        if (location is null) return false;

        // FoodDrop -> PickupLocation is OnDelete(Restrict). Checking here turns
        // what would be an unhandled DbUpdateException (and a dead Blazor
        // circuit) into a plain "you can't do that" in the UI.
        var inUse = await _db.FoodDrops.AnyAsync(f => f.PickupLocationId == locationId);
        if (inUse) return false;

        _db.PickupLocations.Remove(location);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<CookPublicProfile?> GetPublicProfileAsync(int cookUserId)
    {
        var cook = await _db.Users.AsNoTracking()
            .Include(u => u.SellerProfile)
            .FirstOrDefaultAsync(u => u.Id == cookUserId);

        if (cook?.SellerProfile is null) return null;

        var drops = await _db.FoodDrops.AsNoTracking()
            .Include(f => f.Category)
            .Include(f => f.Seller).ThenInclude(s => s!.SellerProfile)
            .Include(f => f.PickupLocation)
            .Where(f => f.SellerId == cookUserId
                        && f.Status != FoodDropStatus.Draft
                        && f.Status != FoodDropStatus.Cancelled)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var upcoming = drops.Where(d => d.PickupWindowEnd >= now).OrderBy(d => d.PickupWindowStart).ToList();
        var past = drops.Where(d => d.PickupWindowEnd < now).OrderByDescending(d => d.PickupWindowStart).Take(8).ToList();

        var reviews = await LoadReviewsAsync(cookUserId);

        return new CookPublicProfile(
            Cook: cook,
            Profile: cook.SellerProfile,
            UpcomingDrops: upcoming,
            PastDrops: past,
            Reviews: reviews,
            RatingAverage: cook.SellerProfile.RatingAverage,
            ReviewCount: reviews.Count,
            CompletedOrders: cook.SellerProfile.CompletedOrders,
            RepeatCustomers: cook.SellerProfile.RepeatCustomers);
    }

    private async Task<List<CookReview>> LoadReviewsAsync(int cookUserId) =>
        await _db.Reviews.AsNoTracking()
            .Include(r => r.Order).ThenInclude(o => o!.Buyer)
            .Include(r => r.Order).ThenInclude(o => o!.FoodDrop)
            .Where(r => r.Order!.FoodDrop!.SellerId == cookUserId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new CookReview(
                r.Id,
                r.OrderId,
                r.Order!.Buyer!.Name,
                r.Order.Buyer.Avatar,
                r.Order.FoodDrop!.Title,
                (r.FoodQuality + r.Value + r.Accuracy + r.PickupExperience) / 4m,
                r.FoodQuality,
                r.Value,
                r.Accuracy,
                r.PickupExperience,
                r.Comment,
                r.CreatedAt,
                r.SellerResponse,
                r.SellerRespondedAt))
            .ToListAsync();

    public Task<List<CookReview>> GetReviewsAsync(int cookUserId) => LoadReviewsAsync(cookUserId);

    public async Task<bool> RespondToReviewAsync(int reviewId, int cookUserId, string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return false;

        var review = await _db.Reviews
            .Include(r => r.Order).ThenInclude(o => o!.FoodDrop)
            .FirstOrDefaultAsync(r => r.Id == reviewId);

        // Only the cook who actually sold the meal may reply to its review.
        if (review?.Order?.FoodDrop is null || review.Order.FoodDrop.SellerId != cookUserId) return false;

        review.SellerResponse = response.Trim();
        review.SellerRespondedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<SellerEarnings> GetEarningsAsync(int cookUserId)
    {
        var profile = await _db.SellerProfiles.AsNoTracking().FirstOrDefaultAsync(sp => sp.UserId == cookUserId);

        var orders = await _db.Orders.AsNoTracking()
            .Include(o => o.FoodDrop)
            .Include(o => o.Buyer)
            .Where(o => o.FoodDrop!.SellerId == cookUserId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        // Money the cook actually keeps: a collected pickup, or a no-show
        // (the food was made and the buyer didn't turn up — not refunded).
        var earned = orders
            .Where(o => o.Status is OrderStatus.Collected or OrderStatus.BuyerNoShow)
            .ToList();

        // Confirmed-but-not-yet-collected money is real but not bankable yet.
        var inFlight = orders
            .Where(o => o.Status is OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.Ready)
            .ToList();

        var refunded = orders.Count(o => o.Status is OrderStatus.Refunded or OrderStatus.SellerCancelled);

        var gross = earned.Sum(o => o.TotalAmount);
        var fees = earned.Sum(o => FeeOn(o.TotalAmount));
        var net = gross - fees;

        // Settled means "included in a real Payout row", not "older than N
        // hours" — so the balance and the payout history can never disagree.
        var unpaid = earned.Where(o => o.PayoutId is null).ToList();
        var available = unpaid.Sum(o => NetOf(o.TotalAmount));

        var payouts = await _db.Payouts.AsNoTracking()
            .Where(p => p.SellerUserId == cookUserId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var paidOut = payouts.Where(p => p.Status == PayoutStatus.Paid).Sum(p => p.Amount);

        var weekStart = DateTime.UtcNow.AddDays(-7);
        var thisWeek = earned.Where(o => (o.CollectedAt ?? o.CreatedAt) >= weekStart).Sum(o => NetOf(o.TotalAmount));

        var rows = earned
            .Concat(inFlight)
            .OrderByDescending(o => o.CollectedAt ?? o.CreatedAt)
            .Take(40)
            .Select(o => new EarningsRow(
                o.Id,
                o.FoodDropId,
                o.FoodDrop?.Title ?? "(removed)",
                o.Buyer?.Name ?? "Someone",
                o.Buyer?.Avatar ?? "🙂",
                o.CollectedAt ?? o.CreatedAt,
                o.TotalAmount,
                FeeOn(o.TotalAmount),
                NetOf(o.TotalAmount),
                Settled: o.PayoutId is not null,
                PayoutId: o.PayoutId))
            .ToList();

        return new SellerEarnings(
            LifetimeGross: gross,
            PlatformFees: fees,
            LifetimeNet: net,
            AvailableToCashOut: available,
            InFlight: inFlight.Sum(o => NetOf(o.TotalAmount)),
            PaidOut: paidOut,
            ThisWeekNet: thisWeek,
            CompletedOrders: earned.Count,
            RefundedOrders: refunded,
            UnpaidOrderCount: unpaid.Count,
            HasPayoutSetup: profile?.HasPayoutSetup ?? false,
            PayoutDestination: profile?.PayoutDestinationLabel ?? "No payout account",
            Rows: rows,
            Payouts: payouts);
    }

    public async Task<bool> SetPayoutDetailsAsync(int userId, string accountName, string bsb, string accountNumber)
    {
        var profile = await _db.SellerProfiles.FirstOrDefaultAsync(sp => sp.UserId == userId);
        if (profile is null) return false;

        var digits = new string((accountNumber ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length < 4) return false;

        profile.PayoutAccountName = accountName.Trim();
        profile.PayoutBsb = new string((bsb ?? "").Where(char.IsDigit).ToArray());
        // Only the last four digits are kept. `digits` goes out of scope here
        // and is never written anywhere — see the note on SellerProfile.
        profile.PayoutAccountLast4 = digits[^4..];
        profile.PayoutReference ??= $"mock_acct_{Guid.NewGuid():N}"[..22];
        profile.PayoutSetupAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<PayoutResult> RequestPayoutAsync(int userId)
    {
        var profile = await _db.SellerProfiles.FirstOrDefaultAsync(sp => sp.UserId == userId);
        if (profile is null) return new PayoutResult(false, null, PayoutError.NoProfile);
        if (!profile.HasPayoutSetup) return new PayoutResult(false, null, PayoutError.NoPayoutMethod);

        var unpaid = await _db.Orders
            .Include(o => o.FoodDrop)
            .Where(o => o.FoodDrop!.SellerId == userId
                        && o.PayoutId == null
                        && (o.Status == OrderStatus.Collected || o.Status == OrderStatus.BuyerNoShow))
            .ToListAsync();

        if (unpaid.Count == 0) return new PayoutResult(false, null, PayoutError.NothingToPayOut);

        var gross = unpaid.Sum(o => o.TotalAmount);
        var fee = unpaid.Sum(o => FeeOn(o.TotalAmount));
        var amount = gross - fee;

        if (amount < MinimumPayout) return new PayoutResult(false, null, PayoutError.BelowMinimum);

        var transfer = await _paymentGateway.PayoutAsync(userId, amount, profile.PayoutDestinationLabel);
        if (!transfer.Success) return new PayoutResult(false, null, PayoutError.TransferFailed);

        var payout = new Payout
        {
            SellerUserId = userId,
            Amount = amount,
            GrossAmount = gross,
            FeeAmount = fee,
            OrderCount = unpaid.Count,
            Status = PayoutStatus.Paid,
            Reference = transfer.Reference,
            Destination = profile.PayoutDestinationLabel,
            CreatedAt = DateTime.UtcNow,
            PaidAt = DateTime.UtcNow,
        };
        _db.Payouts.Add(payout);
        await _db.SaveChangesAsync();

        // Stamping the orders is what stops the same money being paid twice.
        foreach (var order in unpaid) order.PayoutId = payout.Id;
        await _db.SaveChangesAsync();

        return new PayoutResult(true, payout, null);
    }

    public async Task<List<Payout>> GetPayoutsAsync(int userId) =>
        await _db.Payouts.AsNoTracking()
            .Where(p => p.SellerUserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    public async Task RecomputeStatsAsync(int cookUserId)
    {
        var profile = await _db.SellerProfiles.FirstOrDefaultAsync(sp => sp.UserId == cookUserId);
        if (profile is null) return;

        var scores = await _db.Reviews.AsNoTracking()
            .Where(r => r.Order!.FoodDrop!.SellerId == cookUserId)
            .Select(r => (r.FoodQuality + r.Value + r.Accuracy + r.PickupExperience) / 4m)
            .ToListAsync();

        // Null, not 0, with nothing to average — the UI renders null as
        // "New cook" and 0 as a one-star disaster.
        profile.RatingAverage = scores.Count == 0 ? null : Math.Round(scores.Average(), 1);

        var collected = await _db.Orders.AsNoTracking()
            .Where(o => o.FoodDrop!.SellerId == cookUserId && o.Status == OrderStatus.Collected)
            .Select(o => o.BuyerId)
            .ToListAsync();

        profile.CompletedOrders = collected.Count;
        profile.RepeatCustomers = collected.GroupBy(id => id).Count(g => g.Count() > 1);

        await _db.SaveChangesAsync();
    }
}
