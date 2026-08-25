using Marketplace.Web.Models;

namespace Marketplace.Web.Services;

// What a buyer-only user fills in to become a cook. Deliberately short: the
// brief's onboarding principle is "one screen, then you can post" — real
// identity/food-safety verification is a back-office process that happens
// *after* the first drop is drafted, not a wall in front of it.
public record SellerOnboardingRequest(
    string Suburb,
    string Cuisine,
    string Story,
    string LocationLabel,
    string ExactAddress,
    string Instructions,
    double ApproxDistanceKm);

// One review as a buyer would read it on a cook's public page. Flattened
// deliberately — the Review row alone has no reviewer or dish on it, both of
// which live two hops away through Order.
public record CookReview(
    int ReviewId,
    int OrderId,
    string BuyerName,
    string BuyerAvatar,
    string DropTitle,
    decimal Overall,
    int FoodQuality,
    int Value,
    int Accuracy,
    int PickupExperience,
    string Comment,
    DateTime CreatedAt,
    string? SellerResponse,
    DateTime? SellerRespondedAt)
{
    public bool IsAnswered => !string.IsNullOrWhiteSpace(SellerResponse);

    /// A quiet review is the one a cook most needs to see and answer.
    public bool NeedsAttention => !IsAnswered && Overall <= 3.5m;
}

public record CookPublicProfile(
    User Cook,
    SellerProfile Profile,
    List<FoodDrop> UpcomingDrops,
    List<FoodDrop> PastDrops,
    List<CookReview> Reviews,
    decimal? RatingAverage,
    int ReviewCount,
    int CompletedOrders,
    int RepeatCustomers);

public record EarningsRow(
    int OrderId,
    int FoodDropId,
    string DropTitle,
    string BuyerName,
    string BuyerAvatar,
    DateTime At,
    decimal Gross,
    decimal Fee,
    decimal Net,
    bool Settled,
    int? PayoutId);

public record SellerEarnings(
    decimal LifetimeGross,
    decimal PlatformFees,
    decimal LifetimeNet,
    /// Collected money not yet included in a payout — this is what the cook can
    /// actually cash out right now.
    decimal AvailableToCashOut,
    /// Paid for but not yet handed over, so not earnable yet.
    decimal InFlight,
    decimal PaidOut,
    decimal ThisWeekNet,
    int CompletedOrders,
    int RefundedOrders,
    int UnpaidOrderCount,
    bool HasPayoutSetup,
    string PayoutDestination,
    List<EarningsRow> Rows,
    List<Payout> Payouts);

public enum PayoutError
{
    NoProfile,
    NoPayoutMethod,
    NothingToPayOut,
    BelowMinimum,
    TransferFailed,
}

public record PayoutResult(bool Success, Payout? Payout, PayoutError? Error);

public interface ISellerService
{
    Task<SellerProfile?> GetProfileAsync(int userId);
    Task<SellerProfile> CreateProfileAsync(int userId, SellerOnboardingRequest request);
    Task<bool> UpdateProfileAsync(int userId, string suburb, string cuisine, string story);

    Task<List<PickupLocation>> GetPickupLocationsAsync(int userId);
    Task<PickupLocation?> AddPickupLocationAsync(int userId, string label, string suburb, string exactAddress, string instructions, double approxDistanceKm);
    /// Refuses to delete a location that a food drop still points at — the FK
    /// is Restrict, so letting the UI try would surface as a raw DbUpdateException.
    Task<bool> DeletePickupLocationAsync(int userId, int locationId);

    Task<CookPublicProfile?> GetPublicProfileAsync(int cookUserId);
    Task<SellerEarnings> GetEarningsAsync(int cookUserId);

    // --- Getting paid ---

    /// Stores the cook's payout destination. The full account number is used to
    /// derive the last four digits and then discarded — never persisted.
    Task<bool> SetPayoutDetailsAsync(int userId, string accountName, string bsb, string accountNumber);

    /// Cashes out everything collected and not yet paid out, as one Payout
    /// record, and stamps every order it covers so the money can't be paid twice.
    Task<PayoutResult> RequestPayoutAsync(int userId);

    Task<List<Payout>> GetPayoutsAsync(int userId);

    // --- Reputation ---

    Task<List<CookReview>> GetReviewsAsync(int cookUserId);

    /// The cook's public reply to one of their own reviews.
    Task<bool> RespondToReviewAsync(int reviewId, int cookUserId, string response);

    /// Recomputes RatingAverage / CompletedOrders / RepeatCustomers from the
    /// Reviews and Orders tables. Called after every review and every
    /// completed pickup so the numbers on a cook's page are always derived,
    /// never hand-maintained.
    Task RecomputeStatsAsync(int cookUserId);
}
