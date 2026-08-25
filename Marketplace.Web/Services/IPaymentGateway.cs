namespace Marketplace.Web.Services;

public record PaymentChargeResult(bool Success, string Reference, string? FailureReason);
public record PaymentRefundResult(bool Success, string Reference, string? FailureReason);
public record PaymentPayoutResult(bool Success, string Reference, string? FailureReason);

// The seam described in the brief's "Protected Payment" section: swap
// MockPaymentGateway for a real Stripe Connect adapter later and nothing
// above this interface (OrderService, controllers, UI) has to change.
// A real implementation would create a PaymentIntent on the platform
// account with `transfer_data` pointing at the seller's connected account,
// confirm it client-side, and this method would just check its status.
public interface IPaymentGateway
{
    Task<PaymentChargeResult> ChargeAsync(int orderId, decimal amount);

    /// Reverses a previously successful charge. Real Stripe equivalent is a
    /// Refund against the PaymentIntent plus a reversal of the seller
    /// Transfer — which is why cancellation is a gateway concern, not
    /// something OrderService can fake by flipping a status column.
    Task<PaymentRefundResult> RefundAsync(int orderId, string chargeReference, decimal amount);

    /// Money out, to a cook's bank account. This is the third leg of the
    /// marketplace and the one that was missing: buyers could be charged and
    /// refunded, but a cook's balance had no mechanism behind it. Real Stripe
    /// equivalent is a Transfer to the connected account followed by a Payout.
    Task<PaymentPayoutResult> PayoutAsync(int sellerUserId, decimal amount, string destination);
}
