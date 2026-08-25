using Marketplace.Web.Models;

namespace Marketplace.Web.Services;

public enum OrderCreationError
{
    FoodDropNotFound,
    NotOrderable,
    InsufficientPortions,
    CannotOrderOwnListing,
}

public record OrderCreationResult(bool Success, Order? Order, OrderCreationError? Error);

public enum PickupConfirmError
{
    OrderNotFound,
    NotOwnerOfFoodDrop,
    NotConfirmed,
    CodeMismatch,
}

public record PickupConfirmResult(bool Success, PickupConfirmError? Error);

public enum OrderActionError
{
    OrderNotFound,
    NotYours,
    WrongStatus,
    /// The buyer-cancellation window closed at the drop's order deadline —
    /// past it the cook has already shopped and started cooking.
    TooLateToCancel,
    RefundFailed,
    AlreadyReviewed,
    NotCollected,
    InvalidRating,
    PickupWindowNotOver,
}

public record OrderActionResult(bool Success, OrderActionError? Error)
{
    public static readonly OrderActionResult Ok = new(true, null);
    public static OrderActionResult Fail(OrderActionError error) => new(false, error);
}

/// Everything the seller needs to run one batch: the drop, its orders, and
/// the counts the dashboard shows without re-deriving them in the view.
public record DropOrderBoard(
    FoodDrop Drop,
    List<Order> Orders,
    int PortionsSold,
    int ActiveOrders,
    int CollectedOrders,
    int AwaitingPickup,
    decimal GrossRevenue);

public interface IOrderService
{
    /// Validates availability and reserves portions atomically, then creates
    /// a PendingPayment order. Price is always taken from the server-side
    /// FoodDrop record, never from the caller.
    Task<OrderCreationResult> CreateOrderAsync(int buyerId, int foodDropId, int quantity);

    /// Runs the (mocked) payment charge and moves the order to Confirmed on
    /// success. Idempotent — calling it again on an already-Confirmed order
    /// is a no-op success, so a duplicate submit or a page refresh mid-flow
    /// can't double-charge.
    Task<Order?> ConfirmPaymentAsync(int orderId);

    /// Only the seller who owns the food drop can confirm a pickup, and only
    /// with the buyer's exact code — this is deliberately the entire
    /// "security model" for handoff (brief Section 14).
    Task<PickupConfirmResult> ConfirmPickupAsync(int orderId, int requestingSellerId, string code);

    Task<Order?> GetByIdAsync(int orderId, int requestingUserId);
    Task<List<Order>> GetForBuyerAsync(int buyerId);
    Task<List<Order>> GetForSellerAsync(int sellerId);

    // --- Buyer-side lifecycle ---

    /// Free cancellation up to the drop's order deadline: refunds in full and
    /// returns the portions to the batch so someone else can claim them.
    Task<OrderActionResult> CancelByBuyerAsync(int orderId, int buyerId);

    /// Rate a collected order. Creates the Review and recomputes the cook's
    /// aggregate rating — nothing about a cook's score is hand-maintained.
    Task<OrderActionResult> LeaveReviewAsync(
        int orderId, int buyerId, int foodQuality, int value, int accuracy, int pickupExperience, string comment);

    Task<Review?> GetReviewAsync(int orderId);

    // --- Seller-side lifecycle ---

    /// Moves one order along the pickup flow (Confirmed -> Preparing -> Ready).
    Task<OrderActionResult> AdvanceStatusAsync(int orderId, int sellerId, OrderStatus target);

    /// Cook cancels: always a full refund regardless of timing, because the
    /// buyer did nothing wrong. Portions go back to the batch.
    Task<OrderActionResult> CancelBySellerAsync(int orderId, int sellerId, string reason);

    /// The buyer never showed. Only available once the pickup window has
    /// closed, and deliberately *not* refunded — the food was made.
    Task<OrderActionResult> MarkNoShowAsync(int orderId, int sellerId);

    /// The seller's per-batch view: one drop and every order against it.
    Task<DropOrderBoard?> GetDropBoardAsync(int foodDropId, int sellerId);
}
