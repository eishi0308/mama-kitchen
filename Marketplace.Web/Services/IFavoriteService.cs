using Marketplace.Web.Models;

namespace Marketplace.Web.Services;

public interface IFavoriteService
{
    Task<List<FoodDrop>> GetFavoritesAsync(int userId);
    Task<bool> IsFavoriteAsync(int userId, int foodDropId);
    Task ToggleAsync(int userId, int foodDropId);
}
