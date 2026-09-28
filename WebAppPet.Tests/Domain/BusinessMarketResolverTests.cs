using WebAppPet.Domain;
using WebAppPet.Domain.Markets;

namespace WebAppPet.Tests.Domain;

public class BusinessMarketResolverTests
{
    [Theory]
    [InlineData("Cali, Valle del Cauca", BusinessMarket.Colombia)]
    [InlineData("Usaquén, Bogotá", BusinessMarket.Colombia)]
    [InlineData("San Diego, California", BusinessMarket.UnitedStates)]
    [InlineData("Charlotte, NC", BusinessMarket.UnitedStates)]
    [InlineData("Springfield, U.S.", BusinessMarket.UnitedStates)]
    public void Place_hints_match_whole_words_only(string text, BusinessMarket expected)
    {
        Assert.Equal(expected, BusinessMarketResolver.FromText(text));
    }

    [Theory]
    [InlineData(VetProviderKind.None, "Charlotte, NC", null, "CO", "US")]
    [InlineData(VetProviderKind.LocalVet, "Neiva, Huila", null, "US", "CO")]
    [InlineData(VetProviderKind.None, "", null, "US", "US")]
    [InlineData(VetProviderKind.InternationalAdvisor, "Bogotá", "CO", "US", "US")]
    public void A_business_prices_where_it_operates_except_remote_international_advisors(
        VetProviderKind kind, string city, string? license, string ownerCountry, string expected)
    {
        var business = new GroomerProfile { VetProviderKind = kind, City = city, LicenseCountry = license };

        Assert.Equal(expected, BusinessMarketResolver.CountryFor(business, ownerCountry));
    }
}
