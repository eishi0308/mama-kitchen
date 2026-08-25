namespace Marketplace.Web.Services;

public record PaymentChargeResult(bool Success, string Reference, string? FailureReason);
public record PaymentRefundResult(bool Success, string Reference, string? FailureReason);

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
}
