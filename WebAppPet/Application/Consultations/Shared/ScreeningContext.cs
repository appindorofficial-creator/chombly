using WebAppPet.Domain;


namespace WebAppPet.Application.Consultations.Shared;

/// <summary>What the pet and safety step needs to show its form.</summary>
/// <param name="Path">"local" or "intl".</param>
public sealed record ScreeningContext(
    Consultation Consultation,
    string Path,
    List<Pet> Pets,
    string HomeCountryCode,
    string? UserCity);
