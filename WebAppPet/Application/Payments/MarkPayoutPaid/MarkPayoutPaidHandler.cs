using WebAppPet.Application.Payments.Shared;
using WebAppPet.Models;

namespace WebAppPet.Application.Payments.MarkPayoutPaid;

/// <summary>Admin settles a payout with a simulated transfer reference. Already-paid payouts are left as they are.</summary>
public class MarkPayoutPaidHandler
{
    private readonly ProviderPayoutService _payouts;

    public MarkPayoutPaidHandler(ProviderPayoutService payouts) => _payouts = payouts;

    /// <returns>The payout, or null when it does not exist.</returns>
    public Task<ProviderPayout?> HandleAsync(MarkPayoutPaidCommand command, CancellationToken ct = default) =>
        _payouts.MarkPaidAsync(command.PayoutId, command.AdminUserId, ct);
}
