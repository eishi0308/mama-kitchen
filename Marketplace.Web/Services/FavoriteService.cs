using Marketplace.Web.Data;
using Marketplace.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Services;

public class FavoriteService : IFavoriteService
{
    private readonly AppDbContext _db;

    public FavoriteService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<FoodDrop>> GetFavoritesAsync(int userId) =>
        await _db.Favorites
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .Include(f => f.FoodDrop).ThenInclude(fd => fd!.Category)
            .Include(f => f.FoodDrop).ThenInclude(fd => fd!.Seller)
            .Select(f => f.FoodDrop!)
            .ToListAsync();

    public async Task<bool> IsFavoriteAsync(int userId, int foodDropId) =>
        await _db.Favorites.AnyAsync(f => f.UserId == userId && f.FoodDropId == foodDropId);

    public async Task ToggleAsync(int userId, int foodDropId)
    {
        var existing = await _db.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.FoodDropId == foodDropId);
        if (existing is not null)
        {
            _db.Favorites.Remove(existing);
        }
        else
        {
            _db.Favorites.Add(new Favorite { UserId = userId, FoodDropId = foodDropId });
        }
        await _db.SaveChangesAsync();
    }
}
