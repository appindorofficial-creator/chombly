using System.Text.RegularExpressions;
using WebAppPet.Localization;

namespace WebAppPet.Application.Payments.Shared;

/// <summary>
/// Shows the descriptions, refund reasons and failure codes stored on payments (written in English
/// by the system) in the interface language. Free text typed by an admin is shown as written.
/// </summary>
public static partial class PaymentTexts
{
    public static string Description(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return "";
        var text = description.Trim();

        if (text.StartsWith("Deposit · ", StringComparison.Ordinal))
            return CatalogLocalizer.Loc("Anticipo · ", "Deposit · ") + text["Deposit · ".Length..];
        if (BehaviorCase().Match(text) is { Success: true } behavior)
            return CatalogLocalizer.Loc("Caso de comportamiento #", "Behavior case #") + behavior.Groups[1].Value + behavior.Groups[2].Value;
        if (VetConsultation().Match(text) is { Success: true } vet)
            return CatalogLocalizer.Loc("Consulta veterinaria #", "Vet consultation #") + vet.Groups[1].Value + vet.Groups[2].Value;

        return text switch
        {
            "Chombly Care · first month" => CatalogLocalizer.Loc("Chombly Care · primer mes", text),
            "Chombly Care · renewal" => CatalogLocalizer.Loc("Chombly Care · renovación", text),
            "Charge recorded before payment transactions existed" => CatalogLocalizer.Loc(
                "Cobro registrado antes del historial de pagos", text),
            _ => CatalogLocalizer.Text(text)
        };
    }

    public static string RefundReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return "";
        var text = reason.Trim();
        return text switch
        {
            "client_cancelled" => CatalogLocalizer.Loc("la familia canceló", "the family cancelled"),
            "business_rejected" => CatalogLocalizer.Loc("el negocio rechazó la reserva", "the business declined the booking"),
            "Another session of the same booking was declined" => CatalogLocalizer.Loc(
                "se rechazó otra sesión de la misma reserva", text),
            "Appointment cancelled before payment transactions existed" => CatalogLocalizer.Loc(
                "cita cancelada antes del historial de pagos", text),
            _ => text
        };
    }

    public static string FailureCode(string? code) => code switch
    {
        null or "" => "",
        PaymentFailureCodes.CardDeclined => CatalogLocalizer.Loc("tarjeta rechazada", "card declined"),
        PaymentFailureCodes.InsufficientFunds => CatalogLocalizer.Loc("fondos insuficientes", "insufficient funds"),
        PaymentFailureCodes.ExpiredCard => CatalogLocalizer.Loc("tarjeta vencida", "expired card"),
        PaymentFailureCodes.ProcessingError => CatalogLocalizer.Loc("error de procesamiento", "processing error"),
        PaymentFailureCodes.InvalidAmount => CatalogLocalizer.Loc("monto inválido", "invalid amount"),
        _ => code
    };

    [GeneratedRegex(@"^Behavior case #(\d+)(.*)$")]
    private static partial Regex BehaviorCase();

    [GeneratedRegex(@"^Vet consultation #(\d+)(.*)$")]
    private static partial Regex VetConsultation();
}
