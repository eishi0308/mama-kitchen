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
    DateTime CreatedAt);

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
    bool Settled);

public record SellerEarnings(
    decimal LifetimeGross,
    decimal PlatformFees,
    decimal LifetimeNet,
    decimal PendingPayout,
    decimal PaidOut,
    decimal ThisWeekNet,
    int CompletedOrders,
    int RefundedOrders,
    List<EarningsRow> Rows);

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

    /// Recomputes RatingAverage / CompletedOrders / RepeatCustomers from the
    /// Reviews and Orders tables. Called after every review and every
    /// completed pickup so the numbers on a cook's page are always derived,
    /// never hand-maintained.
    Task RecomputeStatsAsync(int cookUserId);
}
