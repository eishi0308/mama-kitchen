using Marketplace.Web.Models;

namespace Marketplace.Web.Services;

public interface IFoodDropService
{
    Task<List<FoodDrop>> SearchAsync(string? query, int? categoryId, decimal? maxPrice, DietaryLabel? dietary);
    Task<FoodDrop?> GetByIdAsync(int id);
    Task<List<FoodDrop>> GetBySellerAsync(int sellerId);
    Task<FoodDrop> CreateAsync(FoodDrop drop);
    Task<bool> CancelAsync(int id, int requestingUserId);
    Task<List<Category>> GetCategoriesAsync();
}
