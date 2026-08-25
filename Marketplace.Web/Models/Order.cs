namespace Marketplace.Web.Models;

public class Order
{
    public int Id { get; set; }

    public int FoodDropId { get; set; }
    public FoodDrop? FoodDrop { get; set; }

    public int BuyerId { get; set; }
    public User? Buyer { get; set; }

    public int Quantity { get; set; }

    // Price snapshotted server-side at order time — never trust a client-sent
    // total (Section 39). If the seller edits the price later, past orders
    // are unaffected.
    public decimal UnitPriceSnapshot { get; set; }
    public decimal TotalAmount { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.PendingPayment;

    // 4-digit pickup code shown to the buyer, entered by the seller at handoff.
    public string PickupCode { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CollectedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    public Payment? Payment { get; set; }
    public Review? Review { get; set; }

    // Set when this order's earnings are included in a payout. Null means the
    // money is still sitting in the cook's balance — which is what makes
    // "available to cash out" a real query rather than a time-based guess.
    public int? PayoutId { get; set; }
    public Payout? Payout { get; set; }
}
