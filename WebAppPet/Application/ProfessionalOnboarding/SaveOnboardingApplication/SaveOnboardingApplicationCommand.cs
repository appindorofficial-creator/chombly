using WebAppPet.Domain;


namespace WebAppPet.Application.ProfessionalOnboarding.SaveOnboardingApplication;

/// <param name="Track">Local and behavior share the US-style form; international has its own.</param>
/// <param name="CountrySearch">International only: the typed country, used when no country code was picked.</param>
/// <param name="Document">Uploaded proof (license, certificate); keeps the previous one when null.</param>
/// <param name="Submit">Send for review after saving the draft.</param>
public sealed record SaveOnboardingApplicationCommand(
    int UserId,
    ProfessionalOnboardingTrack Track,
    OnboardingDetails Details,
    string? CountrySearch,
    OnboardingDocument? Document,
    bool Submit);

/// <param name="LicenseJurisdiction">US state for local and behavior; country for international.</param>
public sealed record OnboardingDetails(
    string? LegalName,
    string? ClinicOrPracticeName,
    string? LicenseNumber,
    string? LicenseJurisdiction,
    DateTime? LicenseExpiry,
    string? Languages,
    string? Specialties,
    string? BreedExpertiseCsv,
    bool AcceptsInternationalClients,
    bool HasPhysicalClinic,
    bool VcprCapable,
    string? DocumentsNote);

/// <param name="SaveAsync">Stores the file and returns its public path.</param>
public sealed record OnboardingDocument(Func<CancellationToken, Task<string>> SaveAsync);

public enum SaveOnboardingApplicationOutcome
{
    NoBusinessProfile,

    /// <summary>International only: the country is not in the catalog. Nothing was saved.</summary>
    CountryNotFound,

    /// <summary>The draft was saved but cannot be submitted without legal name and license jurisdiction.</summary>
    MissingRequired,
    Saved,
    Submitted
}

/// <param name="LicenseJurisdiction">The jurisdiction as stored (the country code for international).</param>
public sealed record SaveOnboardingApplicationResult(SaveOnboardingApplicationOutcome Outcome, string? LicenseJurisdiction);
