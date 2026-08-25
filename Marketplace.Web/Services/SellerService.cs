using Marketplace.Web.Data;
using Marketplace.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Services;

public class SellerService : ISellerService
{
    private readonly AppDbContext _db;

    // The platform's cut. A single constant, referenced by both the earnings
    // page and the order-level breakdown, so the seller can never be shown
    // two different net figures for the same order.
    public const decimal PlatformFeeRate = 0.10m;

    public static decimal FeeOn(decimal gross) => Math.Round(gross * PlatformFeeRate, 2, MidpointRounding.AwayFromZero);
    public static decimal NetOf(decimal gross) => gross - FeeOn(gross);

    public SellerService(AppDbContext db)
    {
        _db = db;
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
                r.CreatedAt))
            .ToListAsync();

    public async Task<SellerEarnings> GetEarningsAsync(int cookUserId)
    {
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

        // A real payout runs on a schedule; this stands in for "settled" by
        // treating anything collected more than 48h ago as already paid out.
        var settleCutoff = DateTime.UtcNow.AddHours(-48);
        var paidOut = earned.Where(o => (o.CollectedAt ?? o.CreatedAt) < settleCutoff).Sum(o => NetOf(o.TotalAmount));
        var pending = net - paidOut + inFlight.Sum(o => NetOf(o.TotalAmount));

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
                Settled: o.Status is OrderStatus.Collected or OrderStatus.BuyerNoShow
                         && (o.CollectedAt ?? o.CreatedAt) < settleCutoff))
            .ToList();

        return new SellerEarnings(
            LifetimeGross: gross,
            PlatformFees: fees,
            LifetimeNet: net,
            PendingPayout: pending,
            PaidOut: paidOut,
            ThisWeekNet: thisWeek,
            CompletedOrders: earned.Count,
            RefundedOrders: refunded,
            Rows: rows);
    }

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
