using WebAppPet.Application.Common;
using WebAppPet.Domain;

namespace WebAppPet.Application.Care.Shared;

public static class CarePlan
{
    public const decimal FallbackMonthlyPrice = 14.99m;

    public static async Task<(ServiceCatalogItem? Item, decimal Price)> CatalogPriceAsync(
        this ServiceCatalogService catalog, CancellationToken ct = default)
    {
        var item = await catalog.GetAsync(ServiceCatalogCodes.ChomblyCare, ct);
        return (item, item?.Price ?? FallbackMonthlyPrice);
    }
}
