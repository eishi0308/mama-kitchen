namespace Marketplace.Web.Models;

// A cuisine, functionally — kept as "Category" since a food drop belongs to
// exactly one, same relationship shape as the original marketplace.
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "🍽️";

    public List<FoodDrop> FoodDrops { get; set; } = new();
}
