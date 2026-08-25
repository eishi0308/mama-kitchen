using Marketplace.Web.Data;
using Marketplace.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    private readonly IPaymentGateway _paymentGateway;
    private readonly ISellerService _sellerService;

    // Statuses where the buyer holds a live claim on a portion. Used by both
    // cancellation (which must give the portion back) and the seller board.
    private static readonly OrderStatus[] LiveStatuses =
    {
        OrderStatus.PendingPayment, OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.Ready,
    };

    public OrderService(AppDbContext db, IPaymentGateway paymentGateway, ISellerService sellerService)
    {
        _db = db;
        _paymentGateway = paymentGateway;
        _sellerService = sellerService;
    }

    public async Task<OrderCreationResult> CreateOrderAsync(int buyerId, int foodDropId, int quantity)
    {
        var drop = await _db.FoodDrops.AsNoTracking().FirstOrDefaultAsync(f => f.Id == foodDropId);
        if (drop is null)
            return new OrderCreationResult(false, null, OrderCreationError.FoodDropNotFound);

        if (drop.SellerId == buyerId)
            return new OrderCreationResult(false, null, OrderCreationError.CannotOrderOwnListing);

        if (!drop.IsOrderable)
            return new OrderCreationResult(false, null, OrderCreationError.NotOrderable);

        // Atomic, race-safe decrement: if two buyers hit "Reserve" on the last
        // portions at the same moment, only the update whose WHERE clause
        // still matches at execution time succeeds — the loser gets a clean
        // InsufficientPortions rather than an oversold order (brief Section 38).
        var reserved = await _db.FoodDrops
            .Where(f => f.Id == foodDropId && f.PortionsRemaining >= quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.PortionsRemaining, f => f.PortionsRemaining - quantity));

        if (reserved == 0)
            return new OrderCreationResult(false, null, OrderCreationError.InsufficientPortions);

        var order = new Order
        {
            FoodDropId = foodDropId,
            BuyerId = buyerId,
            Quantity = quantity,
            UnitPriceSnapshot = drop.Price,
            TotalAmount = drop.Price * quantity,
            Status = OrderStatus.PendingPayment,
            PickupCode = Random.Shared.Next(1000, 10000).ToString(),
            CreatedAt = DateTime.UtcNow,
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        return new OrderCreationResult(true, order, null);
    }

    public async Task<Order?> ConfirmPaymentAsync(int orderId)
    {
        var order = await _db.Orders.Include(o => o.Payment).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null) return null;

        if (order.Status == OrderStatus.Confirmed) return order; // idempotent re-entry

        var charge = await _paymentGateway.ChargeAsync(orderId, order.TotalAmount);

        _db.Payments.Add(new Payment
        {
            OrderId = orderId,
            Provider = "MockConnect",
            Status = charge.Success ? PaymentStatus.Succeeded : PaymentStatus.Failed,
            Amount = order.TotalAmount,
            Reference = charge.Reference,
            ProcessedAt = DateTime.UtcNow,
        });

        if (charge.Success)
        {
            order.Status = OrderStatus.Confirmed;
            order.ConfirmedAt = DateTime.UtcNow;
        }
        else
        {
            // Compensating path: the buyer holds a reservation they never paid
            // for, so the portions must go back to the batch or the drop
            // silently sells out to nobody.
            await ReleasePortionsAsync(order.FoodDropId, order.Quantity);
            order.Status = OrderStatus.Refunded;
            order.CancelledAt = DateTime.UtcNow;
            order.CancellationReason = charge.FailureReason ?? "Payment failed";
        }

        await _db.SaveChangesAsync();
        return order;
    }

    public async Task<PickupConfirmResult> ConfirmPickupAsync(int orderId, int requestingSellerId, string code)
    {
        var order = await _db.Orders.Include(o => o.FoodDrop).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null || order.FoodDrop is null)
            return new PickupConfirmResult(false, PickupConfirmError.OrderNotFound);

        if (order.FoodDrop.SellerId != requestingSellerId)
            return new PickupConfirmResult(false, PickupConfirmError.NotOwnerOfFoodDrop);

        if (order.Status != OrderStatus.Confirmed && order.Status != OrderStatus.Preparing && order.Status != OrderStatus.Ready)
            return new PickupConfirmResult(false, PickupConfirmError.NotConfirmed);

        if (!string.Equals(order.PickupCode, code.Trim(), StringComparison.Ordinal))
            return new PickupConfirmResult(false, PickupConfirmError.CodeMismatch);

        order.Status = OrderStatus.Collected;
        order.CollectedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // A completed pickup changes the cook's public order count.
        await _sellerService.RecomputeStatsAsync(requestingSellerId);
        return new PickupConfirmResult(true, null);
    }

    public async Task<Order?> GetByIdAsync(int orderId, int requestingUserId)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.FoodDrop).ThenInclude(f => f!.PickupLocation)
            .Include(o => o.FoodDrop).ThenInclude(f => f!.Seller).ThenInclude(s => s!.SellerProfile)
            .Include(o => o.FoodDrop).ThenInclude(f => f!.Category)
            .Include(o => o.Buyer)
            .Include(o => o.Payment)
            .Include(o => o.Review)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null) return null;

        // Authorization: only the buyer or the selling cook may view an
        // order — it carries a pickup code and (once confirmed) an exact
        // home address.
        var isBuyer = order.BuyerId == requestingUserId;
        var isSeller = order.FoodDrop?.SellerId == requestingUserId;
        return isBuyer || isSeller ? order : null;
    }

    public async Task<List<Order>> GetForBuyerAsync(int buyerId) =>
        await _db.Orders
            .AsNoTracking()
            .Include(o => o.FoodDrop).ThenInclude(f => f!.Seller)
            .Include(o => o.FoodDrop).ThenInclude(f => f!.PickupLocation)
            .Include(o => o.Review)
            .Where(o => o.BuyerId == buyerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

    public async Task<List<Order>> GetForSellerAsync(int sellerId) =>
        await _db.Orders
            .AsNoTracking()
            .Include(o => o.FoodDrop)
            .Include(o => o.Buyer)
            .Where(o => o.FoodDrop!.SellerId == sellerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

    // ---------------------------------------------------------------------
    // Buyer-side lifecycle
    // ---------------------------------------------------------------------

    public async Task<OrderActionResult> CancelByBuyerAsync(int orderId, int buyerId)
    {
        var order = await _db.Orders
            .Include(o => o.FoodDrop)
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null || order.FoodDrop is null) return OrderActionResult.Fail(OrderActionError.OrderNotFound);
        if (order.BuyerId != buyerId) return OrderActionResult.Fail(OrderActionError.NotYours);
        if (!LiveStatuses.Contains(order.Status)) return OrderActionResult.Fail(OrderActionError.WrongStatus);

        // The cancellation promise the food-drop page makes to the buyer, in
        // one place: free until orders close, not after.
        if (DateTime.UtcNow >= order.FoodDrop.OrderDeadline)
            return OrderActionResult.Fail(OrderActionError.TooLateToCancel);

        if (!await RefundAsync(order)) return OrderActionResult.Fail(OrderActionError.RefundFailed);

        await ReleasePortionsAsync(order.FoodDropId, order.Quantity);
        order.Status = OrderStatus.Refunded;
        order.CancelledAt = DateTime.UtcNow;
        order.CancellationReason = "Cancelled by buyer before orders closed";
        await _db.SaveChangesAsync();
        return OrderActionResult.Ok;
    }

    public async Task<OrderActionResult> LeaveReviewAsync(
        int orderId, int buyerId, int foodQuality, int value, int accuracy, int pickupExperience, string comment)
    {
        foreach (var score in new[] { foodQuality, value, accuracy, pickupExperience })
        {
            if (score is < 1 or > 5) return OrderActionResult.Fail(OrderActionError.InvalidRating);
        }

        var order = await _db.Orders
            .Include(o => o.FoodDrop)
            .Include(o => o.Review)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null || order.FoodDrop is null) return OrderActionResult.Fail(OrderActionError.OrderNotFound);
        if (order.BuyerId != buyerId) return OrderActionResult.Fail(OrderActionError.NotYours);

        // A review is evidence of a real transaction, not a free-floating
        // rating — you can only rate food you actually collected.
        if (order.Status != OrderStatus.Collected) return OrderActionResult.Fail(OrderActionError.NotCollected);
        if (order.Review is not null) return OrderActionResult.Fail(OrderActionError.AlreadyReviewed);

        _db.Reviews.Add(new Review
        {
            OrderId = orderId,
            FoodQuality = foodQuality,
            Value = value,
            Accuracy = accuracy,
            PickupExperience = pickupExperience,
            Comment = (comment ?? "").Trim(),
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        await _sellerService.RecomputeStatsAsync(order.FoodDrop.SellerId);
        return OrderActionResult.Ok;
    }

    public async Task<Review?> GetReviewAsync(int orderId) =>
        await _db.Reviews.AsNoTracking().FirstOrDefaultAsync(r => r.OrderId == orderId);

    // ---------------------------------------------------------------------
    // Seller-side lifecycle
    // ---------------------------------------------------------------------

    public async Task<OrderActionResult> AdvanceStatusAsync(int orderId, int sellerId, OrderStatus target)
    {
        if (target is not (OrderStatus.Preparing or OrderStatus.Ready))
            return OrderActionResult.Fail(OrderActionError.WrongStatus);

        var order = await _db.Orders.Include(o => o.FoodDrop).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null || order.FoodDrop is null) return OrderActionResult.Fail(OrderActionError.OrderNotFound);
        if (order.FoodDrop.SellerId != sellerId) return OrderActionResult.Fail(OrderActionError.NotYours);

        // Only ever move forward along Confirmed -> Preparing -> Ready.
        var rank = (OrderStatus s) => s switch
        {
            OrderStatus.Confirmed => 0,
            OrderStatus.Preparing => 1,
            OrderStatus.Ready => 2,
            _ => -1,
        };
        if (rank(order.Status) < 0 || rank(target) <= rank(order.Status))
            return OrderActionResult.Fail(OrderActionError.WrongStatus);

        order.Status = target;
        await _db.SaveChangesAsync();
        return OrderActionResult.Ok;
    }

    public async Task<OrderActionResult> CancelBySellerAsync(int orderId, int sellerId, string reason)
    {
        var order = await _db.Orders
            .Include(o => o.FoodDrop)
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null || order.FoodDrop is null) return OrderActionResult.Fail(OrderActionError.OrderNotFound);
        if (order.FoodDrop.SellerId != sellerId) return OrderActionResult.Fail(OrderActionError.NotYours);
        if (!LiveStatuses.Contains(order.Status)) return OrderActionResult.Fail(OrderActionError.WrongStatus);

        // No deadline check on purpose: if the cook pulls out, the buyer is
        // made whole no matter how late it is.
        if (!await RefundAsync(order)) return OrderActionResult.Fail(OrderActionError.RefundFailed);

        await ReleasePortionsAsync(order.FoodDropId, order.Quantity);
        order.Status = OrderStatus.SellerCancelled;
        order.CancelledAt = DateTime.UtcNow;
        order.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled by the cook" : reason.Trim();
        await _db.SaveChangesAsync();
        return OrderActionResult.Ok;
    }

    public async Task<OrderActionResult> MarkNoShowAsync(int orderId, int sellerId)
    {
        var order = await _db.Orders.Include(o => o.FoodDrop).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null || order.FoodDrop is null) return OrderActionResult.Fail(OrderActionError.OrderNotFound);
        if (order.FoodDrop.SellerId != sellerId) return OrderActionResult.Fail(OrderActionError.NotYours);
        if (order.Status is not (OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.Ready))
            return OrderActionResult.Fail(OrderActionError.WrongStatus);

        // Guard against a cook marking a no-show while the buyer could still
        // legitimately turn up.
        if (DateTime.UtcNow < order.FoodDrop.PickupWindowEnd)
            return OrderActionResult.Fail(OrderActionError.PickupWindowNotOver);

        order.Status = OrderStatus.BuyerNoShow;
        order.CancelledAt = DateTime.UtcNow;
        order.CancellationReason = "Buyer did not collect within the pickup window";
        // Deliberately no refund and no portion release — the food was cooked.
        await _db.SaveChangesAsync();
        return OrderActionResult.Ok;
    }

    public async Task<DropOrderBoard?> GetDropBoardAsync(int foodDropId, int sellerId)
    {
        var drop = await _db.FoodDrops.AsNoTracking()
            .Include(f => f.Category)
            .Include(f => f.PickupLocation)
            .Include(f => f.Seller)
            .FirstOrDefaultAsync(f => f.Id == foodDropId);

        if (drop is null || drop.SellerId != sellerId) return null;

        var orders = await _db.Orders.AsNoTracking()
            .Include(o => o.Buyer)
            .Where(o => o.FoodDropId == foodDropId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var live = orders.Where(o => LiveStatuses.Contains(o.Status)).ToList();
        var collected = orders.Where(o => o.Status == OrderStatus.Collected).ToList();

        return new DropOrderBoard(
            Drop: drop,
            Orders: orders,
            PortionsSold: live.Sum(o => o.Quantity) + collected.Sum(o => o.Quantity)
                          + orders.Where(o => o.Status == OrderStatus.BuyerNoShow).Sum(o => o.Quantity),
            ActiveOrders: live.Count,
            CollectedOrders: collected.Count,
            AwaitingPickup: orders.Count(o => o.Status is OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.Ready),
            GrossRevenue: collected.Concat(orders.Where(o => o.Status == OrderStatus.BuyerNoShow)).Sum(o => o.TotalAmount));
    }

    // ---------------------------------------------------------------------
    // Shared helpers
    // ---------------------------------------------------------------------

    /// Gives reserved portions back to the batch, capped at PortionsTotal so a
    /// double-cancel can never inflate a drop beyond what the cook offered.
    private async Task ReleasePortionsAsync(int foodDropId, int quantity)
    {
        await _db.FoodDrops
            .Where(f => f.Id == foodDropId)
            .ExecuteUpdateAsync(s => s.SetProperty(
                f => f.PortionsRemaining,
                f => f.PortionsRemaining + quantity > f.PortionsTotal ? f.PortionsTotal : f.PortionsRemaining + quantity));
    }

    /// Refunds through the gateway and records it on the Payment row. An order
    /// that was never successfully charged (still PendingPayment) has nothing
    /// to reverse, which counts as success rather than an error.
    private async Task<bool> RefundAsync(Order order)
    {
        var payment = order.Payment ?? await _db.Payments.FirstOrDefaultAsync(p => p.OrderId == order.Id);
        if (payment is null || payment.Status != PaymentStatus.Succeeded) return true;

        var refund = await _paymentGateway.RefundAsync(order.Id, payment.Reference, payment.Amount);
        if (!refund.Success) return false;

        payment.Status = PaymentStatus.Refunded;
        payment.ProcessedAt = DateTime.UtcNow;
        return true;
    }
}
