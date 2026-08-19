namespace Marketplace.Web.Models;

// Payment is deliberately its own table, separate from Order, because a real
// integration (Stripe Connect) owns this data independently — a PaymentIntent
// id, its status, and later a Transfer/payout reference. Swapping
// MockPaymentGateway for a real StripePaymentGateway (see Services/) only
// ever touches this shape, never Order or the rest of the app.
public class Payment
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public string Provider { get; set; } = "MockConnect"; // "StripeConnect" once wired for real
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public decimal Amount { get; set; }
    public string Reference { get; set; } = ""; // stand-in for a Stripe PaymentIntent id

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}
