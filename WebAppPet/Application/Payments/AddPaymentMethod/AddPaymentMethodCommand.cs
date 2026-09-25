using WebAppPet.Application.Payments.Shared;

namespace WebAppPet.Application.Payments.AddPaymentMethod;

public sealed record AddPaymentMethodCommand(
    int UserId,
    string? CardNumber,
    string? Expiry,
    string? Cvv,
    string? HolderName,
    bool MakeDefault);

public sealed record AddPaymentMethodResult(CardValidator.FailReason Error)
{
    public bool Success => Error == CardValidator.FailReason.None;
}
