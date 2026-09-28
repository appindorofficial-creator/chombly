using WebAppPet.Application.Common;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;

namespace WebAppPet.Application.Care.Shared;

public static class CarePlan
{
    public const decimal FallbackMonthlyPrice = 14.99m;
    public const decimal ColombiaMonthlyPrice = 50_000m;

    /// <summary>The catalog price is in USD; Colombia pays its own peso price.</summary>
    public static decimal MonthlyPrice(decimal usdPrice, string? countryIso = null) =>
        MarketCountry.IsUnitedStates(countryIso ?? AppTimeZones.CurrentCountryCode) ? usdPrice : ColombiaMonthlyPrice;

    public static async Task<(ServiceCatalogItem? Item, decimal Price)> CatalogPriceAsync(
        this ServiceCatalogService catalog, CancellationToken ct = default)
    {
        var item = await catalog.GetAsync(ServiceCatalogCodes.ChomblyCare, ct);
        return (item, MonthlyPrice(item?.Price ?? FallbackMonthlyPrice));
    }
}
