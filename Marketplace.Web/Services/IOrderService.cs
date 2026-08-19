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
}
