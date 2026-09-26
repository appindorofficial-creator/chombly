using WebAppPet.Models;

namespace WebAppPet.Application.Payments.Shared;

public sealed record GatewayChargeRequest(decimal Amount, string Currency, PaymentMethod Card, string Description);

public sealed record GatewayChargeResult(bool Succeeded, string Reference, string? FailureCode);

public sealed record GatewayRefundResult(bool Succeeded, string Reference, string? FailureCode);

/// <summary>
/// Card processor (simulated today; Wompi, Mercado Pago, PayU or Stripe later).
/// Implementations never throw for a declined card: they return <c>Succeeded = false</c> with a code.
/// </summary>
public interface IPaymentGateway
{
    string Name { get; }

    Task<GatewayChargeResult> ChargeAsync(GatewayChargeRequest request, CancellationToken ct = default);

    Task<GatewayRefundResult> RefundAsync(string chargeReference, decimal amount, CancellationToken ct = default);
}
