namespace Marketplace.Web.Models;

// Money leaving the platform, out to a cook's bank account. Its own table for
// the same reason Payment is: a real integration (Stripe Connect Transfers /
// Payouts) owns this lifecycle independently, and a cook needs a durable record
// of what they were actually paid — not a figure recomputed on every page load.
public class Payout
{
    public int Id { get; set; }

    public int SellerUserId { get; set; }
    public User? Seller { get; set; }

    public decimal Amount { get; set; }        // net, after the platform fee
    public decimal GrossAmount { get; set; }   // what buyers paid, before the fee
    public decimal FeeAmount { get; set; }
    public int OrderCount { get; set; }

    public PayoutStatus Status { get; set; } = PayoutStatus.Pending;

    // Stand-in for a Stripe Transfer id.
    public string Reference { get; set; } = "";

    // Masked destination, e.g. "Bank ••••4821". Deliberately the only account
    // detail stored on the payout itself.
    public string Destination { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    public List<Order> Orders { get; set; } = new();
}
