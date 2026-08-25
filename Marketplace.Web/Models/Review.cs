namespace Marketplace.Web.Models;

// Only ever created for a Collected order (see OrderService.LeaveReviewAsync) —
// reviews aren't a free-floating rating system.
public class Review
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int FoodQuality { get; set; } // 1-5
    public int Value { get; set; } // 1-5
    public int Accuracy { get; set; } // 1-5
    public int PickupExperience { get; set; } // 1-5
    public string Comment { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // A cook's public reply. One per review, and only the cook who sold the
    // meal can write it — a review nobody can answer is a review, not a
    // conversation, and quiet failures never get explained.
    public string? SellerResponse { get; set; }
    public DateTime? SellerRespondedAt { get; set; }
}
