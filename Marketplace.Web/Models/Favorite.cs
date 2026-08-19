namespace Marketplace.Web.Models;

public class Favorite
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int FoodDropId { get; set; }
    public FoodDrop? FoodDrop { get; set; }
}
