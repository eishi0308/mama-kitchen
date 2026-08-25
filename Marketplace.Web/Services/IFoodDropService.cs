using Marketplace.Web.Models;

namespace Marketplace.Web.Services;

/// One row of the cook's dashboard: the batch plus the numbers they actually
/// need at a glance — how many to cook, how many still to hand over.
public record SellerDropSummary(
    FoodDrop Drop,
    int PortionsSold,
    int AwaitingPickup,
    int CollectedCount,
    decimal GrossRevenue)
{
    public int PortionsToCook => PortionsSold;
    public decimal NetRevenue => GrossRevenue - SellerService.FeeOn(GrossRevenue);
}

/// The cook's whole kitchen in one query — used by the dashboard so it never
/// fires N+1 order lookups while rendering a list of drops.
public record KitchenSummary(
    List<SellerDropSummary> Today,
    List<SellerDropSummary> Upcoming,
    List<SellerDropSummary> Past,
    List<SellerDropSummary> Drafts,
    int ActionsNeeded,
    decimal PendingPayout,
    int AwaitingPickupTotal);

public enum DropEditError
{
    NotFound,
    NotYours,
    /// Portions can't be cut below what buyers have already reserved.
    BelowSoldPortions,
    InvalidWindow,
    Locked,
}

public record DropEditResult(bool Success, DropEditError? Error)
{
    public static readonly DropEditResult Ok = new(true, null);
    public static DropEditResult Fail(DropEditError e) => new(false, e);
}

public interface IFoodDropService
{
    Task<List<FoodDrop>> SearchAsync(string? query, int? categoryId, decimal? maxPrice, DietaryLabel? dietary);
    Task<FoodDrop?> GetByIdAsync(int id);
    Task<List<FoodDrop>> GetBySellerAsync(int sellerId);
    Task<FoodDrop> CreateAsync(FoodDrop drop);
    Task<List<Category>> GetCategoriesAsync();

    /// A drop the cook owns, including Draft/Cancelled ones the public
    /// SearchAsync deliberately hides.
    Task<FoodDrop?> GetForEditAsync(int id, int sellerId);

    Task<DropEditResult> UpdateAsync(int id, int sellerId, FoodDrop edited);

    /// Drives the batch through Published -> OrderingClosed -> Preparing ->
    /// Ready -> Completed, cascading each stage onto every live order so the
    /// buyer's status tracker actually moves.
    Task<DropEditResult> SetStageAsync(int id, int sellerId, FoodDropStatus stage);

    /// Cancels the batch AND refunds every live order against it — a cook
    /// pulling a drop must never leave buyers charged for food that isn't coming.
    Task<DropEditResult> CancelAsync(int id, int requestingUserId, string reason);

    Task<KitchenSummary> GetKitchenSummaryAsync(int sellerId);

    /// Other drops by the same cook, for the "more from this kitchen" rail.
    Task<List<FoodDrop>> GetMoreFromCookAsync(int sellerId, int excludeDropId, int take = 4);
}
