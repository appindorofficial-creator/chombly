using WebAppPet.Application.Payments.Shared;
using WebAppPet.Models;

namespace WebAppPet.Application.Payments.GetProviderPayouts;

public sealed record GetProviderPayoutsQuery(int ProviderUserId);

public sealed record ProviderPayoutsView(
    List<ProviderCompensationRule> Rules,
    List<ProviderPayout> History,
    List<ProviderPayoutService.ProviderPaymentRow> RecentPayments);
