using Marketplace.Web.Data;
using Marketplace.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Services;

public class FoodDropService : IFoodDropService
{
    private readonly AppDbContext _db;
    private readonly IOrderService _orderService;

    private static readonly OrderStatus[] LiveStatuses =
    {
        OrderStatus.PendingPayment, OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.Ready,
    };

    public FoodDropService(AppDbContext db, IOrderService orderService)
    {
        _db = db;
        _orderService = orderService;
    }

    public async Task<List<FoodDrop>> SearchAsync(string? query, int? categoryId, decimal? maxPrice, DietaryLabel? dietary)
    {
        // Draft and Cancelled drops are never shown publicly; everything else
        // (including sold-out / orders-closed) stays visible with its status
        // badge — a beautiful listing that's sold out still builds trust and
        // converts to "notify me next time" (brief Section 24).
        var drops = _db.FoodDrops
            .AsNoTracking()
            .Include(f => f.Category)
            .Include(f => f.Seller).ThenInclude(s => s!.SellerProfile)
            .Include(f => f.PickupLocation)
            .Where(f => f.Status != FoodDropStatus.Draft && f.Status != FoodDropStatus.Cancelled)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim().ToLower();
            // Cook name and cuisine are searchable too — "search dish, cuisine
            // or cook" is what the box promises, so it has to look there.
            drops = drops.Where(f =>
                f.Title.ToLower().Contains(q) ||
                f.Description.ToLower().Contains(q) ||
                f.Seller!.Name.ToLower().Contains(q) ||
                f.Category!.Name.ToLower().Contains(q));
        }

        if (categoryId.HasValue)
            drops = drops.Where(f => f.CategoryId == categoryId);

        if (maxPrice.HasValue)
            drops = drops.Where(f => f.Price <= maxPrice);

        if (dietary.HasValue && dietary.Value != DietaryLabel.None)
            drops = drops.Where(f => (f.Dietary & dietary.Value) == dietary.Value);

        return await drops.OrderBy(f => f.PickupWindowStart).ToListAsync();
    }

    public async Task<FoodDrop?> GetByIdAsync(int id) =>
        await _db.FoodDrops
            .AsNoTracking()
            .Include(f => f.Category)
            .Include(f => f.Seller).ThenInclude(s => s!.SellerProfile)
            .Include(f => f.PickupLocation)
            .FirstOrDefaultAsync(f => f.Id == id);

    public async Task<FoodDrop?> GetForEditAsync(int id, int sellerId) =>
        await _db.FoodDrops
            .AsNoTracking()
            .Include(f => f.Category)
            .Include(f => f.PickupLocation)
            .FirstOrDefaultAsync(f => f.Id == id && f.SellerId == sellerId);

    public async Task<List<FoodDrop>> GetBySellerAsync(int sellerId) =>
        await _db.FoodDrops
            .AsNoTracking()
            .Include(f => f.Category)
            .Include(f => f.PickupLocation)
            .Where(f => f.SellerId == sellerId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

    public async Task<List<FoodDrop>> GetMoreFromCookAsync(int sellerId, int excludeDropId, int take = 4) =>
        await _db.FoodDrops
            .AsNoTracking()
            .Include(f => f.Category)
            .Include(f => f.Seller).ThenInclude(s => s!.SellerProfile)
            .Include(f => f.PickupLocation)
            .Where(f => f.SellerId == sellerId
                        && f.Id != excludeDropId
                        && f.Status != FoodDropStatus.Draft
                        && f.Status != FoodDropStatus.Cancelled
                        && f.PickupWindowEnd >= DateTime.UtcNow)
            .OrderBy(f => f.PickupWindowStart)
            .Take(take)
            .ToListAsync();

    public async Task<FoodDrop> CreateAsync(FoodDrop drop)
    {
        drop.CreatedAt = DateTime.UtcNow;
        drop.PortionsRemaining = drop.PortionsTotal;
        if (drop.Status != FoodDropStatus.Draft) drop.Status = FoodDropStatus.Published;
        _db.FoodDrops.Add(drop);
        await _db.SaveChangesAsync();
        return drop;
    }

    public async Task<DropEditResult> UpdateAsync(int id, int sellerId, FoodDrop edited)
    {
        var drop = await _db.FoodDrops.FirstOrDefaultAsync(f => f.Id == id);
        if (drop is null) return DropEditResult.Fail(DropEditError.NotFound);
        if (drop.SellerId != sellerId) return DropEditResult.Fail(DropEditError.NotYours);
        if (drop.Status is FoodDropStatus.Cancelled or FoodDropStatus.Completed)
            return DropEditResult.Fail(DropEditError.Locked);

        if (edited.PickupWindowEnd <= edited.PickupWindowStart || edited.OrderDeadline > edited.PickupWindowEnd)
            return DropEditResult.Fail(DropEditError.InvalidWindow);

        // Portions already claimed by buyers set the floor. Without this a cook
        // could edit a 15-portion batch down to 2 after 9 people had paid.
        var claimed = drop.PortionsTotal - drop.PortionsRemaining;
        if (edited.PortionsTotal < claimed) return DropEditResult.Fail(DropEditError.BelowSoldPortions);

        drop.Title = edited.Title;
        drop.Description = edited.Description;
        drop.ImageEmoji = edited.ImageEmoji;
        drop.ImageUrl = edited.ImageUrl;
        // Editing the price never rewrites an existing order: every Order
        // carries its own UnitPriceSnapshot taken at reservation time.
        drop.Price = edited.Price;
        drop.PortionsRemaining = edited.PortionsTotal - claimed;
        drop.PortionsTotal = edited.PortionsTotal;
        drop.OrderDeadline = edited.OrderDeadline;
        drop.PickupWindowStart = edited.PickupWindowStart;
        drop.PickupWindowEnd = edited.PickupWindowEnd;
        drop.Ingredients = edited.Ingredients;
        drop.Allergens = edited.Allergens;
        drop.Dietary = edited.Dietary;
        drop.CategoryId = edited.CategoryId;
        drop.PickupLocationId = edited.PickupLocationId;

        await _db.SaveChangesAsync();
        return DropEditResult.Ok;
    }

    public async Task<DropEditResult> SetStageAsync(int id, int sellerId, FoodDropStatus stage)
    {
        var drop = await _db.FoodDrops.FirstOrDefaultAsync(f => f.Id == id);
        if (drop is null) return DropEditResult.Fail(DropEditError.NotFound);
        if (drop.SellerId != sellerId) return DropEditResult.Fail(DropEditError.NotYours);
        if (drop.Status is FoodDropStatus.Cancelled) return DropEditResult.Fail(DropEditError.Locked);

        drop.Status = stage;

        // Cascade onto live orders so the buyer's tracker moves in step with
        // the cook's actions. Without this the Preparing/Ready order states are
        // unreachable and the buyer's progress dots are decorative.
        var cascade = stage switch
        {
            FoodDropStatus.Preparing => OrderStatus.Preparing,
            FoodDropStatus.Ready => OrderStatus.Ready,
            _ => (OrderStatus?)null,
        };

        if (cascade is OrderStatus target)
        {
            var orders = await _db.Orders
                .Where(o => o.FoodDropId == id && (o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Preparing))
                .ToListAsync();

            foreach (var order in orders)
            {
                // Never walk an order backwards (a Ready order stays Ready if
                // the cook taps "Start cooking" a second time).
                if (target == OrderStatus.Preparing && order.Status != OrderStatus.Confirmed) continue;
                order.Status = target;
            }
        }

        // Closing orders early stops new reservations without touching
        // existing ones — IsOrderable already gates on Status == Published.
        await _db.SaveChangesAsync();
        return DropEditResult.Ok;
    }

    public async Task<DropEditResult> CancelAsync(int id, int requestingUserId, string reason)
    {
        var drop = await _db.FoodDrops.FirstOrDefaultAsync(f => f.Id == id);
        if (drop is null) return DropEditResult.Fail(DropEditError.NotFound);
        if (drop.SellerId != requestingUserId) return DropEditResult.Fail(DropEditError.NotYours);

        // Refund everyone still holding a live order before the batch
        // disappears — cancelling the drop but leaving buyers charged would be
        // the single worst bug this app could ship.
        var liveOrderIds = await _db.Orders
            .Where(o => o.FoodDropId == id && LiveStatuses.Contains(o.Status))
            .Select(o => o.Id)
            .ToListAsync();

        foreach (var orderId in liveOrderIds)
        {
            await _orderService.CancelBySellerAsync(
                orderId, requestingUserId,
                string.IsNullOrWhiteSpace(reason) ? "The cook cancelled this food drop" : reason.Trim());
        }

        drop.Status = FoodDropStatus.Cancelled;
        await _db.SaveChangesAsync();
        return DropEditResult.Ok;
    }

    public async Task<List<Category>> GetCategoriesAsync() =>
        await _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();

    public async Task<KitchenSummary> GetKitchenSummaryAsync(int sellerId)
    {
        var drops = await _db.FoodDrops.AsNoTracking()
            .Include(f => f.Category)
            .Include(f => f.PickupLocation)
            .Where(f => f.SellerId == sellerId)
            .ToListAsync();

        var dropIds = drops.Select(d => d.Id).ToList();

        // One query for every order across every drop — the dashboard renders
        // a list, so a per-drop lookup here would be a classic N+1.
        var orders = await _db.Orders.AsNoTracking()
            .Where(o => dropIds.Contains(o.FoodDropId))
            .ToListAsync();

        var byDrop = orders.GroupBy(o => o.FoodDropId).ToDictionary(g => g.Key, g => g.ToList());

        SellerDropSummary Summarise(FoodDrop d)
        {
            var os = byDrop.GetValueOrDefault(d.Id, new List<Order>());
            var earning = os.Where(o => o.Status is OrderStatus.Collected or OrderStatus.BuyerNoShow).ToList();
            return new SellerDropSummary(
                Drop: d,
                PortionsSold: os.Where(o => LiveStatuses.Contains(o.Status) || o.Status is OrderStatus.Collected or OrderStatus.BuyerNoShow)
                                .Sum(o => o.Quantity),
                AwaitingPickup: os.Count(o => o.Status is OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.Ready),
                CollectedCount: os.Count(o => o.Status == OrderStatus.Collected),
                GrossRevenue: earning.Sum(o => o.TotalAmount));
        }

        var now = DateTime.UtcNow;
        var summaries = drops.Select(Summarise).ToList();

        var drafts = summaries.Where(s => s.Drop.Status == FoodDropStatus.Draft).ToList();
        var cancelled = summaries.Where(s => s.Drop.Status == FoodDropStatus.Cancelled).ToList();
        var active = summaries.Except(drafts).Except(cancelled).ToList();

        // "Today" means the pickup window hasn't closed yet and starts within
        // the next 15 hours — the same hours-until framing Discover uses, so a
        // 9pm pickup doesn't jump to "tomorrow" the moment the clock rolls over.
        var today = active
            .Where(s => s.Drop.PickupWindowEnd >= now && (s.Drop.PickupWindowStart - now).TotalHours < 15)
            .OrderBy(s => s.Drop.PickupWindowStart).ToList();

        var upcoming = active
            .Where(s => s.Drop.PickupWindowEnd >= now && (s.Drop.PickupWindowStart - now).TotalHours >= 15)
            .OrderBy(s => s.Drop.PickupWindowStart).ToList();

        var past = active
            .Where(s => s.Drop.PickupWindowEnd < now)
            .OrderByDescending(s => s.Drop.PickupWindowStart)
            .Concat(cancelled.OrderByDescending(s => s.Drop.PickupWindowStart))
            .ToList();

        var awaiting = active.Sum(s => s.AwaitingPickup);

        var pendingPayout = orders
            .Where(o => o.Status is OrderStatus.Collected or OrderStatus.BuyerNoShow or OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.Ready)
            .Sum(o => SellerService.NetOf(o.TotalAmount));

        return new KitchenSummary(
            Today: today,
            Upcoming: upcoming,
            Past: past,
            Drafts: drafts,
            // What the cook has to actually do right now: hand food over today,
            // or deal with a batch whose pickup window has closed with orders
            // still outstanding.
            ActionsNeeded: today.Sum(s => s.AwaitingPickup)
                           + active.Count(s => s.Drop.PickupWindowEnd < now && s.AwaitingPickup > 0),
            PendingPayout: pendingPayout,
            AwaitingPickupTotal: awaiting);
    }
}
