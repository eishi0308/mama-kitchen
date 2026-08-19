using Marketplace.Web.Data;
using Marketplace.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Services;

public class FoodDropService : IFoodDropService
{
    private readonly AppDbContext _db;

    public FoodDropService(AppDbContext db)
    {
        _db = db;
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
            .Include(f => f.Seller)
            .Include(f => f.PickupLocation)
            .Where(f => f.Status != FoodDropStatus.Draft && f.Status != FoodDropStatus.Cancelled)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim().ToLower();
            drops = drops.Where(f => f.Title.ToLower().Contains(q) || f.Description.ToLower().Contains(q));
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

    public async Task<List<FoodDrop>> GetBySellerAsync(int sellerId) =>
        await _db.FoodDrops
            .AsNoTracking()
            .Include(f => f.Category)
            .Where(f => f.SellerId == sellerId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

    public async Task<FoodDrop> CreateAsync(FoodDrop drop)
    {
        drop.CreatedAt = DateTime.UtcNow;
        drop.PortionsRemaining = drop.PortionsTotal;
        drop.Status = FoodDropStatus.Published;
        _db.FoodDrops.Add(drop);
        await _db.SaveChangesAsync();
        return drop;
    }

    public async Task<bool> CancelAsync(int id, int requestingUserId)
    {
        var drop = await _db.FoodDrops.FindAsync(id);
        if (drop is null || drop.SellerId != requestingUserId) return false;
        drop.Status = FoodDropStatus.Cancelled;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<Category>> GetCategoriesAsync() =>
        await _db.Categories.OrderBy(c => c.Name).ToListAsync();
}
