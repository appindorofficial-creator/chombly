using WebAppPet.Localization;

namespace WebAppPet.Application.Payments.Shared;

public static class PaymentFailureCodes
{
    public const string CardDeclined = "card_declined";
    public const string InsufficientFunds = "insufficient_funds";
    public const string ExpiredCard = "expired_card";
    public const string ProcessingError = "processing_error";
    public const string InvalidAmount = "invalid_amount";

    public static string Message(string? code) => code switch
    {
        InsufficientFunds => CatalogLocalizer.Loc(
            "Pago rechazado: fondos insuficientes. Prueba con otra tarjeta.",
            "Payment declined: insufficient funds. Try another card."),
        ExpiredCard => CatalogLocalizer.Loc(
            "Pago rechazado: la tarjeta está vencida. Actualízala o usa otra.",
            "Payment declined: the card has expired. Update it or use another one."),
        ProcessingError => CatalogLocalizer.Loc(
            "No pudimos procesar el pago. Intenta de nuevo en unos minutos.",
            "We couldn't process the payment. Try again in a few minutes."),
        _ => CatalogLocalizer.Loc(
            "Pago rechazado por el banco. Prueba con otra tarjeta.",
            "Payment declined by the bank. Try another card.")
    };
}
