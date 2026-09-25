using WebAppPet.Models;

namespace WebAppPet.Application.Payments.GeneratePayout;

public sealed record GeneratePayoutCommand(int ProviderUserId, DateTime PeriodStartUtc, DateTime PeriodEndUtc);

public enum GeneratePayoutError
{
    None,
    InvalidPeriod,
    PeriodOverlap
}

/// <param name="Payout">Null unless <see cref="Success"/>.</param>
public sealed record GeneratePayoutResult(GeneratePayoutError Error, ProviderPayout? Payout)
{
    public bool Success => Error == GeneratePayoutError.None;
}
