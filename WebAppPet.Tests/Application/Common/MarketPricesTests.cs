using WebAppPet.Application.Common;
using WebAppPet.Domain;

namespace WebAppPet.Tests.Application.Common;

public class MarketPricesTests
{
    [Theory]
    [InlineData(ServiceCatalogCodes.ChomblyCare, 50_000)]
    [InlineData(ServiceCatalogCodes.VetIntl30, 90_000)]
    [InlineData(ServiceCatalogCodes.BehaviorSession, 60_000)]
    [InlineData(ServiceCatalogCodes.VetLocal30, 70)]
    public void Colombia_pays_its_peso_prices(string code, int expected)
    {
        Assert.Equal(expected, MarketPrices.ForCatalog(code, 70m, "CO"));
    }

    [Theory]
    [InlineData(ServiceCatalogCodes.ChomblyCare)]
    [InlineData(ServiceCatalogCodes.VetIntl30)]
    [InlineData(ServiceCatalogCodes.BehaviorSession)]
    public void The_United_States_pays_the_catalog_price(string code)
    {
        Assert.Equal(30m, MarketPrices.ForCatalog(code, 30m, "US"));
    }

    [Theory]
    [InlineData("CO", "CO", 40_000, 40_000)]
    [InlineData("CO", "US", 30, 90_000)]
    [InlineData("US", "US", 35, 35)]
    [InlineData("US", "CO", 70_000, 30)]
    public void A_providers_own_price_only_applies_to_families_in_its_accounts_country(
        string family, string providerAccount, int startingPrice, int expected)
    {
        var provider = new GroomerProfile { StartingPrice = startingPrice };
        var marketPrice = MarketPrices.ForCatalog(ServiceCatalogCodes.VetIntl30, 30m, family);

        Assert.Equal(expected, MarketPrices.ForProvider(provider, providerAccount, marketPrice, family));
    }

    [Fact]
    public void Where_the_provider_is_licensed_does_not_decide_the_currency_of_its_price()
    {
        var licensedInColombia = new GroomerProfile { LicenseCountry = "CO", City = "Bogotá", StartingPrice = 30 };

        Assert.Equal(90_000m, MarketPrices.ForProvider(licensedInColombia, "US", 90_000m, "CO"));
    }

    [Fact]
    public void Without_its_own_price_a_provider_charges_the_market_price()
    {
        Assert.Equal(90_000m, MarketPrices.ForProvider(new GroomerProfile(), "CO", 90_000m, "CO"));
    }
}
