namespace Marketplace.Web.Models;

// A scheduled batch of home-cooked food (the core marketplace unit — see
// product brief Section 3). Replaces the old goods-marketplace `Listing`.
public class FoodDrop
{
    public int Id { get; set; }

    public string Title { get; set; } = ""; // dish name, e.g. "Thai Green Curry"
    public string Description { get; set; } = "";
    public string ImageEmoji { get; set; } = "🍲"; // stand-in for a photo — no upload/storage needed yet
    public string? ImageUrl { get; set; }

    public decimal Price { get; set; } // per portion, AUD
    public int PortionsTotal { get; set; }
    public int PortionsRemaining { get; set; }

    public DateTime OrderDeadline { get; set; } // "orders close at"
    public DateTime PickupWindowStart { get; set; }
    public DateTime PickupWindowEnd { get; set; }

    public string Ingredients { get; set; } = "";
    public string Allergens { get; set; } = "";
    public DietaryLabel Dietary { get; set; } = DietaryLabel.None;

    public FoodDropStatus Status { get; set; } = FoodDropStatus.Published;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int CategoryId { get; set; } // cuisine
    public Category? Category { get; set; }

    public int SellerId { get; set; }
    public User? Seller { get; set; }

    public int PickupLocationId { get; set; }
    public PickupLocation? PickupLocation { get; set; }

    public List<Favorite> Favorites { get; set; } = new();
    public List<Message> Messages { get; set; } = new();
    public List<Order> Orders { get; set; } = new();

    // Derived, not stored — a drop is orderable only while these all hold.
    public bool IsOrderable =>
        Status == FoodDropStatus.Published &&
        PortionsRemaining > 0 &&
        DateTime.UtcNow < OrderDeadline;
}
