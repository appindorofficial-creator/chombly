using WebAppPet.Application.Behavior.GetBehaviorProviders;

namespace WebAppPet.Application.Behavior.BookBehaviorSession;

/// <param name="Date">Selected day as yyyy-MM-dd.</param>
/// <param name="When">Legacy "hoy"/"manana", normalized into <paramref name="Date"/>.</param>
/// <param name="PaymentMethodId">Card to charge; the default card when null.</param>
/// <param name="IpAddress">Stored with the consents.</param>
public sealed record BookBehaviorSessionCommand(
    int ClientId,
    int CaseId,
    int ProviderId,
    string? Date,
    string? When,
    string? Slot,
    bool AcceptTerms,
    int? PaymentMethodId,
    string? IpAddress,
    string? UserAgent);

public enum BookBehaviorSessionOutcome
{
    /// <summary>The case cannot pick a specialist; see <see cref="BehaviorProviderOptions.Redirect"/>.</summary>
    Redirect,
    TermsNotAccepted,
    NoDate,
    NoSlot,
    NoProvider,
    PastTime,
    SlotTaken,

    /// <summary>No dogs selected or the behavior session is missing from the catalog.</summary>
    Incomplete,
    NoPaymentMethod,
    PaymentDeclined,
    Booked
}

/// <param name="Options">The form to show again.</param>
/// <param name="PaymentError">Why the card was declined, when <see cref="BookBehaviorSessionOutcome.PaymentDeclined"/>.</param>
public sealed record BookBehaviorSessionResult(
    BookBehaviorSessionOutcome Outcome,
    BehaviorProviderOptions Options,
    string? PaymentError = null);
