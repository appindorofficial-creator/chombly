using WebAppPet.Models;

namespace WebAppPet.Application.Payments.GetAdminPayouts;

/// <param name="RefreshTotals">Recompute every payout's totals before listing.</param>
public sealed record GetAdminPayoutsQuery(bool RefreshTotals);

/// <param name="Refreshed">How many payouts changed totals; 0 when not refreshed.</param>
public sealed record AdminPayoutsView(int Refreshed, List<ProviderPayout> Pending, List<ProviderPayout> Recent);
