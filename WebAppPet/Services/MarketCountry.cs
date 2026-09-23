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
