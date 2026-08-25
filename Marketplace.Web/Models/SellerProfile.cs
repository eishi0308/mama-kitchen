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

    // --- Payout details ---
    // The full account number is NEVER stored. The form takes it, derives the
    // last four digits for display, and discards the rest — a real build hands
    // it straight to Stripe and keeps only the returned account token. Storing
    // full bank details here would be the worst decision in the codebase.
    public string? PayoutAccountName { get; set; }
    public string? PayoutBsb { get; set; }
    public string? PayoutAccountLast4 { get; set; }
    public string? PayoutReference { get; set; } // stand-in for a Stripe connected-account id
    public DateTime? PayoutSetupAt { get; set; }

    /// A cook can post and sell without this, but can't be paid until it's set.
    public bool HasPayoutSetup => !string.IsNullOrWhiteSpace(PayoutAccountLast4);

    public string PayoutDestinationLabel =>
        HasPayoutSetup ? $"Bank ••••{PayoutAccountLast4}" : "No payout account";

    public List<PickupLocation> PickupLocations { get; set; } = new();
    public List<FoodDrop> FoodDrops { get; set; } = new();
}
