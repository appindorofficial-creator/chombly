using WebAppPet.Models;

namespace WebAppPet.Services;

/// <summary>Primary launch markets for professional onboarding UX.</summary>
public enum BusinessMarket
{
    Unknown = 0,
    UnitedStates = 1,
    Colombia = 2
}

/// <summary>
/// Infers US vs Colombia from business profile text + coordinates.
/// Used to order Professional Onboarding tracks (not a hard legal gate).
/// </summary>
public static class BusinessMarketResolver
{
    private static readonly string[] ColombiaPlaceHints =
    {
        "colombia", "colombiano", "colombiana",
        "bogota", "bogotá", "medellin", "medellín", "cali", "neiva", "cartagena",
        "barranquilla", "bucaramanga", "pereira", "manizales", "ibague", "ibagué",
        "huila", "antioquia", "cundinamarca", "valle del cauca", "atlantico", "atlántico",
        "santander", "tolima", "risaralda", "quindio", "quindío", "narino", "nariño"
    };

    private static readonly string[] UsPlaceHints =
    {
        "united states", "usa", "u.s.a", "u.s.", "ee.uu", "eeuu", "estados unidos",
        "north carolina", "south carolina", "california", "florida", "texas", "new york",
        "georgia", "virginia", "tennessee", "charlotte", "miami", "houston", "los angeles"
    };

    public static BusinessMarket Resolve(GroomerProfile? profile)
    {
        if (profile is null) return BusinessMarket.Unknown;

        var fromLicense = FromIso(profile.LicenseCountry);
        if (fromLicense != BusinessMarket.Unknown) return fromLicense;

        var blob = $"{profile.City} {profile.Address}".Trim();
        var fromText = FromText(blob);
        if (fromText != BusinessMarket.Unknown) return fromText;

        return FromCoordinates(profile.Latitude, profile.Longitude);
    }

    public static BusinessMarket FromIso(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso)) return BusinessMarket.Unknown;
        var t = iso.Trim().ToUpperInvariant();
        if (t is "CO" or "COL") return BusinessMarket.Colombia;
        if (t is "US" or "USA" or "UM") return BusinessMarket.UnitedStates;
        return BusinessMarket.Unknown;
    }

    public static BusinessMarket FromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return BusinessMarket.Unknown;
        var lower = text.Trim().ToLowerInvariant();

        foreach (var hint in ColombiaPlaceHints)
        {
            if (lower.Contains(hint, StringComparison.Ordinal))
                return BusinessMarket.Colombia;
        }

        foreach (var hint in UsPlaceHints)
        {
            if (lower.Contains(hint, StringComparison.Ordinal))
                return BusinessMarket.UnitedStates;
        }

        var usState = GeoHelper.GuessUsStateFromCity(text);
        if (usState is not null and not "Other")
            return BusinessMarket.UnitedStates;

        return BusinessMarket.Unknown;
    }

    public static BusinessMarket FromCoordinates(double lat, double lng)
    {
        if (lat == 0 && lng == 0) return BusinessMarket.Unknown;

        // Colombia mainland approx
        if (lat is >= -4.5 and <= 13.5 && lng is >= -79.5 and <= -66.5)
            return BusinessMarket.Colombia;

        // Continental US approx
        if (lat is >= 24 and <= 49.5 && lng is >= -125 and <= -66)
            return BusinessMarket.UnitedStates;

        return BusinessMarket.Unknown;
    }

    /// <summary>Infers client market from profile city + GPS (clients, not businesses).</summary>
    public static BusinessMarket ResolveUser(string? city, double? lat, double? lng)
    {
        if (lat is double la && lng is double lo)
        {
            var fromCoords = FromCoordinates(la, lo);
            if (fromCoords != BusinessMarket.Unknown) return fromCoords;
        }

        return FromText(city);
    }

    public static string DefaultInternationalIso(BusinessMarket market) =>
        market == BusinessMarket.UnitedStates ? "MX" : "CO";

    /// <summary>
    /// Local marketplace browse: only businesses in the client's home launch market (CO↔CO, US↔US).
    /// Unknown / other countries are excluded from category listings (they surface in virtual intl match).
    /// </summary>
    public static bool MatchesHomeMarket(GroomerProfile? profile, string? homeCountryIso)
    {
        var home = MarketCountry.ToMarket(homeCountryIso);
        var biz = Resolve(profile);
        if (biz == BusinessMarket.Unknown) return false;
        return biz == home;
    }

    public static IEnumerable<GroomerProfile> FilterHomeMarket(
        IEnumerable<GroomerProfile> profiles,
        string? homeCountryIso)
        => profiles.Where(g => MatchesHomeMarket(g, homeCountryIso));
}
