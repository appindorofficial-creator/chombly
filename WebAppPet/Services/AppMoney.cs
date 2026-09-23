using System.Globalization;

namespace WebAppPet.Services;

/// <summary>
/// Home-market money display: USD for United States, COP (peso) for Colombia.
/// Uses <see cref="AppTimeZones.CurrentCountryCode"/> when country is omitted.
/// </summary>
public static class AppMoney
{
    public static string Code(string? countryIso = null) =>
        MarketCountry.IsUnitedStates(countryIso ?? AppTimeZones.CurrentCountryCode) ? "USD" : "COP";

    public static string NameEs(string? countryIso = null) =>
        MarketCountry.IsUnitedStates(countryIso ?? AppTimeZones.CurrentCountryCode) ? "dólar" : "peso";

    public static string NameEn(string? countryIso = null) =>
        MarketCountry.IsUnitedStates(countryIso ?? AppTimeZones.CurrentCountryCode) ? "dollar" : "peso";

    /// <summary>Short label for UI (e.g. earnings headers).</summary>
    public static string CodeLabel(string? countryIso = null) => Code(countryIso);

    /// <summary>
    /// Formatted amount for the home market.
    /// US → <c>US$12.50</c>; Colombia → <c>$45.000</c> (peso, es-CO grouping).
    /// </summary>
    public static string Format(decimal amount, string? countryIso = null)
    {
        var iso = countryIso ?? AppTimeZones.CurrentCountryCode;
        if (MarketCountry.IsUnitedStates(iso))
        {
            var n = amount == decimal.Truncate(amount)
                ? amount.ToString("0", CultureInfo.InvariantCulture)
                : amount.ToString("0.00", CultureInfo.InvariantCulture);
            return "US$" + n;
        }

        var co = CultureInfo.GetCultureInfo("es-CO");
        var body = amount == decimal.Truncate(amount)
            ? amount.ToString("N0", co)
            : amount.ToString("N2", co);
        return "$" + body;
    }

    /// <summary>Amount plus ISO code: <c>US$12.50 USD</c> / <c>$45.000 COP</c>.</summary>
    public static string FormatWithCode(decimal amount, string? countryIso = null)
    {
        var iso = countryIso ?? AppTimeZones.CurrentCountryCode;
        return $"{Format(amount, iso)} {Code(iso)}";
    }
}
