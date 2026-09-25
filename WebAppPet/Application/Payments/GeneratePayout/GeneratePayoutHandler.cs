using WebAppPet.Application.Payments.Shared;

namespace WebAppPet.Application.Payments.GeneratePayout;

/// <summary>
/// The provider asks for a pending summary of a period. Rejected when a summary with items
/// already covers part of the period.
/// </summary>
public class GeneratePayoutHandler
{
    private readonly ProviderPayoutService _payouts;

    public GeneratePayoutHandler(ProviderPayoutService payouts) => _payouts = payouts;

    public async Task<GeneratePayoutResult> HandleAsync(GeneratePayoutCommand command, CancellationToken ct = default)
    {
        if (command.PeriodEndUtc <= command.PeriodStartUtc)
            return new GeneratePayoutResult(GeneratePayoutError.InvalidPeriod, null);

        try
        {
            var payout = await _payouts.CreatePendingPayoutAsync(
                command.ProviderUserId,
                command.PeriodStartUtc,
                command.PeriodEndUtc,
                command.ProviderUserId,
                ct);
            return new GeneratePayoutResult(GeneratePayoutError.None, payout);
        }
        catch (PayoutPeriodOverlapException)
        {
            return new GeneratePayoutResult(GeneratePayoutError.PeriodOverlap, null);
        }
    }
}
