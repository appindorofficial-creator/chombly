using WebAppPet.Localization;

namespace WebAppPet.Application.Payments.Shared;

public static class PaymentFailureCodes
{
    public const string CardDeclined = "card_declined";
    public const string InsufficientFunds = "insufficient_funds";
    public const string ExpiredCard = "expired_card";
    public const string ProcessingError = "processing_error";
    public const string InvalidAmount = "invalid_amount";

    public static string Message(string? code)
    {
        var (es, en) = Texts(code);
        return CatalogLocalizer.Loc(es, en);
    }

    /// <summary>For text that is saved (notifications) and translated when shown.</summary>
    public static string SpanishMessage(string? code) => Texts(code).Es;

    private static (string Es, string En) Texts(string? code) => code switch
    {
        InsufficientFunds => (
            "Pago rechazado: fondos insuficientes. Prueba con otra tarjeta.",
            "Payment declined: insufficient funds. Try another card."),
        ExpiredCard => (
            "Pago rechazado: la tarjeta está vencida. Actualízala o usa otra.",
            "Payment declined: the card has expired. Update it or use another one."),
        ProcessingError => (
            "No pudimos procesar el pago. Intenta de nuevo en unos minutos.",
            "We couldn't process the payment. Try again in a few minutes."),
        _ => (
            "Pago rechazado por el banco. Prueba con otra tarjeta.",
            "Payment declined by the bank. Try another card.")
    };
}
