using WebAppPet.Models;

namespace WebAppPet.Application.Payments.GeneratePayout;

public sealed record GeneratePayoutCommand(int ProviderUserId, DateTime PeriodStartUtc, DateTime PeriodEndUtc);

/// <param name="Payout">Null when <see cref="InvalidPeriod"/> is true.</param>
public sealed record GeneratePayoutResult(bool InvalidPeriod, ProviderPayout? Payout);
