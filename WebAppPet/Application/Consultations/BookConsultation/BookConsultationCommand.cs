using WebAppPet.Application.Consultations.GetConsultationCheckout;

namespace WebAppPet.Application.Consultations.BookConsultation;

/// <param name="AcceptScope">Accepted the scope of the service (and the international orientation consent).</param>
/// <param name="PaymentMethodId">Card to charge; the default card when null.</param>
/// <param name="WantsCare">Pay with a Chombly Care quick consult when one is left.</param>
/// <param name="IpAddress">Stored with the consents.</param>
public sealed record BookConsultationCommand(
    int ClientId,
    int ConsultationId,
    bool AcceptTerms,
    bool AcceptScope,
    bool AcceptMedia,
    int? PaymentMethodId,
    bool WantsCare,
    string? IpAddress,
    string? UserAgent);

public enum BookConsultationOutcome
{
    NotFound,

    /// <summary>Terms or scope not accepted, or the consultation is missing pet, provider or time.</summary>
    Incomplete,
    NoPaymentMethod,
    CareBenefitFailed,
    Booked
}

public sealed record BookConsultationResult(BookConsultationOutcome Outcome, CheckoutDetails? Details, bool UsingCare);
