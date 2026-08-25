namespace Marketplace.Web.Services;

// Development-only gateway: no real card networks, no real money movement.
// It simulates the latency of a real charge so the checkout UI's loading
// state is honest, and always succeeds — this app never fabricates a
// *failed* real transaction any more than it fabricates a successful one.
// Production wiring for this interface is real Stripe Connect, gated behind
// its own configuration section (see appsettings — intentionally absent here).
public class MockPaymentGateway : IPaymentGateway
{
    public async Task<PaymentChargeResult> ChargeAsync(int orderId, decimal amount)
    {
        await Task.Delay(400); // stand-in for a real network round-trip
        var reference = $"mock_pi_{Guid.NewGuid():N}"[..24];
        return new PaymentChargeResult(Success: true, Reference: reference, FailureReason: null);
    }

    public async Task<PaymentRefundResult> RefundAsync(int orderId, string chargeReference, decimal amount)
    {
        await Task.Delay(300);
        var reference = $"mock_re_{Guid.NewGuid():N}"[..24];
        return new PaymentRefundResult(Success: true, Reference: reference, FailureReason: null);
    }
}
