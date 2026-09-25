using WebAppPet.Application.Payments.Shared;

namespace WebAppPet.Application.Payments.GetAdminPayouts;

/// <summary>Payouts waiting to be paid, plus the most recent ones of every provider.</summary>
public class GetAdminPayoutsHandler
{
    public const int RecentShown = 40;

    private readonly ProviderPayoutService _payouts;

    public GetAdminPayoutsHandler(ProviderPayoutService payouts) => _payouts = payouts;

    public async Task<AdminPayoutsView> HandleAsync(GetAdminPayoutsQuery query, CancellationToken ct = default)
    {
        var refreshed = query.RefreshTotals ? await _payouts.RefreshAllPayoutTotalsAsync(ct) : 0;
        return new AdminPayoutsView(
            refreshed,
            await _payouts.ListPendingAsync(ct),
            await _payouts.ListAllAsync(RecentShown, ct));
    }
}
