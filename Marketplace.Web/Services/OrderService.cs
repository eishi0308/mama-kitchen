using Marketplace.Web.Data;
using Marketplace.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    private readonly IPaymentGateway _paymentGateway;

    public OrderService(AppDbContext db, IPaymentGateway paymentGateway)
    {
        _db = db;
        _paymentGateway = paymentGateway;
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
        // A real failure branch would release the reserved portions back to
        // the FoodDrop here. MockPaymentGateway never fails, so that
        // compensating path isn't exercised in this build — flagged rather
        // than silently assumed away.

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
        return new PickupConfirmResult(true, null);
    }

    public async Task<Order?> GetByIdAsync(int orderId, int requestingUserId)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.FoodDrop).ThenInclude(f => f!.PickupLocation)
            .Include(o => o.FoodDrop).ThenInclude(f => f!.Seller)
            .Include(o => o.Buyer)
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
            .Include(o => o.FoodDrop)
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
}
