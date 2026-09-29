using WebAppPet.Application.Payments.Shared;
using WebAppPet.Domain;

namespace WebAppPet.Application.Payments.GetProviderPayouts;

public sealed record GetProviderPayoutsQuery(int ProviderUserId);

/// <param name="Rule">The rule that sets the provider's commission, or null when the platform fallback applies.</param>
/// <param name="CommissionPercent">Share of each service total Chombly keeps.</param>
/// <param name="Currency">Currency the payout history amounts are expressed in.</param>
public sealed record ProviderPayoutsView(
    ProviderCompensationRule? Rule,
    decimal CommissionPercent,
    List<ProviderPayout> History,
    List<ProviderPayoutService.ProviderPaymentRow> RecentPayments,
    string Currency);
