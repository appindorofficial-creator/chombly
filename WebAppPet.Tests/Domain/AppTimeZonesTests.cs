using WebAppPet.Domain.Markets;

namespace WebAppPet.Tests.Domain;

public class AppTimeZonesTests
{
    [Fact]
    public void Without_a_request_the_market_is_colombia()
    {
        Assert.Equal(BusinessMarket.Colombia, AppTimeZones.CurrentMarket);
        Assert.Equal("CO", AppTimeZones.CurrentCountryCode);
    }

    [Fact]
    public void The_request_market_applies_until_its_scope_ends()
    {
        using (AppTimeZones.UseRequest(BusinessMarket.UnitedStates, "US"))
        {
            Assert.Equal(BusinessMarket.UnitedStates, AppTimeZones.CurrentMarket);
            Assert.Equal("US", AppTimeZones.CurrentCountryCode);
            Assert.Equal(AppTimeZones.NewYorkId, AppTimeZones.TimeZoneId);
        }

        Assert.Equal(BusinessMarket.Colombia, AppTimeZones.CurrentMarket);
    }

    [Fact]
    public async Task The_request_market_flows_into_awaited_work()
    {
        using var _ = AppTimeZones.UseRequest(BusinessMarket.UnitedStates, "US");

        var seen = await Task.Run(async () =>
        {
            await Task.Yield();
            return AppTimeZones.CurrentCountryCode;
        });

        Assert.Equal("US", seen);
    }

    [Fact]
    public void A_forced_market_wins_over_the_request_for_the_zone_and_the_country()
    {
        using var request = AppTimeZones.UseRequest(BusinessMarket.Colombia, "CO");

        using (AppTimeZones.UseMarket(BusinessMarket.UnitedStates))
        {
            Assert.Equal(BusinessMarket.UnitedStates, AppTimeZones.CurrentMarket);
            Assert.Equal("US", AppTimeZones.CurrentCountryCode);
        }

        Assert.Equal("CO", AppTimeZones.CurrentCountryCode);
    }
}
