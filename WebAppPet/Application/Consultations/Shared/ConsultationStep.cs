namespace WebAppPet.Application.Consultations.Shared;

/// <summary>The screen of the consultation flow the client should go to next.</summary>
public enum ConsultationStep
{
    VetHome,
    Pet,
    Eligibility,
    LocalProviders,
    Checkout,
    IntlHome,
    IntlMatches,
    ChomblyCare
}
