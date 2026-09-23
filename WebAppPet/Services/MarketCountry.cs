namespace WebAppPet.Services;

/// <summary>
/// Canonical home-country flag for clients and businesses.
/// Persist on <see cref="Models.AppUser.CountryCode"/> at registration; reuse across the app.
/// </summary>
public static class MarketCountry
{
    public const string DefaultIso = "CO";
    public const string HttpItemKey = "AppCountryCode";

    /// <summary>Launch markets only (expand when opening new countries).</summary>
    public static readonly HashSet<string> LaunchIsos = new(StringComparer.OrdinalIgnoreCase)
    {
        "CO", "US"
    };

    public static string Normalize(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso)) return DefaultIso;
        var t = iso.Trim().ToUpperInvariant();
        if (t is "COL" or "COLOMBIA") return "CO";
        if (t is "USA" or "UM") return "US";
        if (t.Length >= 2) t = t[..2];
        return LaunchIsos.Contains(t) ? t : DefaultIso;
    }

    public static bool IsLaunchMarket(string? iso) => LaunchIsos.Contains(Normalize(iso));

    public static BusinessMarket ToMarket(string? iso) =>
        Normalize(iso) == "US" ? BusinessMarket.UnitedStates : BusinessMarket.Colombia;

    public static string FromMarket(BusinessMarket market) =>
        market == BusinessMarket.UnitedStates ? "US" : "CO";

    public static bool IsUnitedStates(string? iso) => Normalize(iso) == "US";

    /// <summary>US-state licensed teleconsult (VCPR) is only for United States home market.</summary>
    public static bool AllowsUsLocalTeleconsult(string? homeCountryIso) => IsUnitedStates(homeCountryIso);

    /// <summary>Consent copy for virtual/international orientation (US Rx line only when home is US).</summary>
    public static (string Es, string En) VirtualScopeConsent(string? homeCountryIso, bool international)
    {
        if (IsUnitedStates(homeCountryIso))
        {
            return international
                ? (
                    "Entiendo que es orientación internacional: no receta EE.UU., no emergencia, no sustituye veterinario local.",
                    "I understand this is international guidance: no U.S. Rx, not an emergency, does not replace a local vet.")
                : (
                    "Entiendo que es orientación virtual: no receta EE.UU., no emergencia, no sustituye veterinario local.",
                    "I understand this is virtual guidance: no U.S. Rx, not an emergency, does not replace a local vet.");
        }

        return international
            ? (
                "Entiendo que es orientación virtual internacional: no es una emergencia y no sustituye la consulta con tu veterinario local.",
                "I understand this is international virtual guidance: it is not an emergency and does not replace a visit with your local vet.")
            : (
                "Entiendo que es orientación virtual: no es una emergencia y no sustituye la consulta con tu veterinario local.",
                "I understand this is virtual guidance: it is not an emergency and does not replace a visit with your local vet.");
    }

    /// <summary>Derives ISO from location when the user has not stored a country yet.</summary>
    public static string ResolveFromLocation(string? city, double? lat, double? lng)
    {
        var market = BusinessMarketResolver.ResolveUser(city, lat, lng);
        return market == BusinessMarket.Unknown ? DefaultIso : FromMarket(market);
    }

    /// <summary>Sets <see cref="Models.AppUser.CountryCode"/> from coords/city (registration / profile).</summary>
    public static void ApplyFromLocation(Models.AppUser user, string? city = null, double? lat = null, double? lng = null)
    {
        user.CountryCode = ResolveFromLocation(
            city ?? user.City,
            lat ?? user.Latitude,
            lng ?? user.Longitude);
    }

    /// <summary>Prefer stored flag; fall back to location inference.</summary>
    public static string ResolveForUser(string? storedCountry, string? city, double? lat, double? lng)
    {
        if (!string.IsNullOrWhiteSpace(storedCountry))
            return Normalize(storedCountry);
        return ResolveFromLocation(city, lat, lng);
    }
}
