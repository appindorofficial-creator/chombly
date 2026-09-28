using WebAppPet.Application.Payments.Shared;
using WebAppPet.Domain;

namespace WebAppPet.Application.Payments.GetProviderPayouts;

public sealed record GetProviderPayoutsQuery(int ProviderUserId);

/// <param name="Currency">Currency the payout history amounts are expressed in.</param>
public sealed record ProviderPayoutsView(
    List<ProviderCompensationRule> Rules,
    List<ProviderPayout> History,
    List<ProviderPayoutService.ProviderPaymentRow> RecentPayments,
    string Currency);
