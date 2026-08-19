namespace Marketplace.Web.Models;

public class SellerProfile
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public string Suburb { get; set; } = "";
    public string Story { get; set; } = ""; // "I grew up in Chiang Mai and cook the dishes my family made at home."
    public string Cuisine { get; set; } = ""; // headline cuisine, e.g. "Thai home cook"
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.NotStarted;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Seed-only display stats standing in for a real aggregate over
    // Reviews/Orders once enough of either accumulate. A brand-new seller
    // should read RatingAverage as null ("New cook") rather than 0.
    public decimal? RatingAverage { get; set; }
    public int CompletedOrders { get; set; }
    public int RepeatCustomers { get; set; }

    public List<PickupLocation> PickupLocations { get; set; } = new();
    public List<FoodDrop> FoodDrops { get; set; } = new();
}
