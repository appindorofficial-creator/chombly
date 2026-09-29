using WebAppPet.Domain;
using WebAppPet.Domain.Markets;

namespace WebAppPet.Application.Common;

/// <summary>
/// Catalog prices are in USD, but charges use the family's market currency, so Colombia has its own peso prices.
/// </summary>
public static class MarketPrices
{
    public const decimal ColombiaCare = 50_000m;
    public const decimal ColombiaIntlConsult = 90_000m;
    public const decimal ColombiaBehaviorSession = 60_000m;

    public static decimal ForCatalog(ServiceCatalogItem item, string? countryIso = null) =>
        ForCatalog(item.Code, item.Price, countryIso);

    public static decimal ForCatalog(string code, decimal usdPrice, string? countryIso = null)
    {
        if (MarketCountry.IsUnitedStates(countryIso ?? AppTimeZones.CurrentCountryCode))
            return usdPrice;

        return code switch
        {
            ServiceCatalogCodes.ChomblyCare => ColombiaCare,
            ServiceCatalogCodes.VetIntl30 => ColombiaIntlConsult,
            ServiceCatalogCodes.BehaviorSession => ColombiaBehaviorSession,
            _ => usdPrice
        };
    }

    /// <summary>
    /// Providers set their starting price in the currency of <paramref name="providerCountry"/> (see
    /// <see cref="BusinessMarketResolver.CountryFor"/>), so it only applies to families in that country;
    /// anyone else pays the market price.
    /// </summary>
    public static decimal ForProvider(GroomerProfile? provider, string? providerCountry, decimal marketPrice, string? countryIso = null)
    {
        if (provider is null || provider.StartingPrice <= 0)
            return marketPrice;

        var familyCountry = MarketCountry.Normalize(countryIso ?? AppTimeZones.CurrentCountryCode);
        return MarketCountry.Normalize(providerCountry) == familyCountry ? provider.StartingPrice : marketPrice;
    }
}
