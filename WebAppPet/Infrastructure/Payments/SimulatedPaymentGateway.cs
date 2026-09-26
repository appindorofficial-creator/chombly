using WebAppPet.Application.Payments.Shared;

namespace WebAppPet.Infrastructure.Payments;

/// <summary>
/// Approves every card except the test endings below (same numbers as Stripe's test cards),
/// so declines can be exercised end to end without a real processor.
/// </summary>
public class SimulatedPaymentGateway : IPaymentGateway
{
    public const string GatewayName = "simulated";

    public static readonly IReadOnlyDictionary<string, string> DeclinedLast4 = new Dictionary<string, string>
    {
        ["0002"] = PaymentFailureCodes.CardDeclined,
        ["9995"] = PaymentFailureCodes.InsufficientFunds,
        ["0069"] = PaymentFailureCodes.ExpiredCard,
        ["0119"] = PaymentFailureCodes.ProcessingError
    };

    public string Name => GatewayName;

    public Task<GatewayChargeResult> ChargeAsync(GatewayChargeRequest request, CancellationToken ct = default)
    {
        var reference = NewReference("sim_ch_");
        if (request.Amount <= 0)
            return Task.FromResult(new GatewayChargeResult(false, reference, PaymentFailureCodes.InvalidAmount));
        if (DeclinedLast4.TryGetValue(request.Card.Last4, out var code))
            return Task.FromResult(new GatewayChargeResult(false, reference, code));
        if (IsExpired(request.Card.ExpMonth, request.Card.ExpYear))
            return Task.FromResult(new GatewayChargeResult(false, reference, PaymentFailureCodes.ExpiredCard));
        return Task.FromResult(new GatewayChargeResult(true, reference, null));
    }

    public Task<GatewayRefundResult> RefundAsync(string chargeReference, decimal amount, CancellationToken ct = default) =>
        Task.FromResult(new GatewayRefundResult(true, NewReference("sim_re_"), null));

    private static bool IsExpired(int month, int year)
    {
        if (month is < 1 or > 12 || year <= 0) return false;
        var fullYear = year < 100 ? 2000 + year : year;
        var now = DateTime.UtcNow;
        return fullYear < now.Year || (fullYear == now.Year && month < now.Month);
    }

    private static string NewReference(string prefix) => prefix + Guid.NewGuid().ToString("N")[..24];
}
