using WebAppPet.Application.Payments.Shared;

namespace WebAppPet.Application.Payments.GetProviderPayouts;

/// <summary>
/// The provider's commission rules, payout history and recent paid bookings.
/// Recomputes the provider's payout totals first so they include the latest bookings.
/// </summary>
public class GetProviderPayoutsHandler
{
    private readonly ProviderPayoutService _payouts;

    public GetProviderPayoutsHandler(ProviderPayoutService payouts) => _payouts = payouts;

    public async Task<ProviderPayoutsView> HandleAsync(GetProviderPayoutsQuery query, CancellationToken ct = default)
    {
        var uid = query.ProviderUserId;
        await _payouts.RefreshPayoutTotalsAsync(uid, ct);
        return new ProviderPayoutsView(
            await _payouts.ListRulesForProviderAsync(uid, ct),
            await _payouts.ListForProviderAsync(uid, ct),
            await _payouts.ListRecentFamilyPaymentsAsync(uid, ct: ct));
    }
}
