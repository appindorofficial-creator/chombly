namespace WebAppPet.Application.Businesses.SearchBusinesses;

/// <param name="CategorySlug">Primary category of the listing (walkers, trainers, daycare).</param>
/// <param name="CountryCode">Home market; businesses from other markets are hidden.</param>
/// <param name="PetSpecies">Species of every selected pet. No pets means no results.</param>
/// <param name="RequiredSpecies">Species every result must accept even beyond the pets (walkers: dogs).</param>
/// <param name="OpenOn">Only businesses whose agenda is open that calendar day.</param>
public sealed record SearchBusinessesQuery(
    string CategorySlug,
    string? CountryCode,
    IReadOnlyCollection<string> PetSpecies,
    string? RequiredSpecies = null,
    DateTime? OpenOn = null,
    double? UserLatitude = null,
    double? UserLongitude = null);
