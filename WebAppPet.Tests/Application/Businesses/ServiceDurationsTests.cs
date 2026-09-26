using WebAppPet.Application.Businesses.Shared;

namespace WebAppPet.Tests.Application.Businesses;

public class ServiceDurationsTests
{
    [Theory]
    [InlineData("Paseo 30 min", 30)]
    [InlineData("30-min walk", 30)]
    [InlineData("Paseo 90 minutos", 90)]
    [InlineData("Paseo 60 min", 60)]
    [InlineData("Paseo largo", 60)]
    [InlineData("Baño básico", 60)]
    [InlineData("", 60)]
    [InlineData(null, 60)]
    [InlineData("Paseo 0 min", 60)]
    [InlineData("Plan 2000 min", 60)]
    public void Reads_minutes_from_the_name_or_defaults_to_an_hour(string? name, int expected) =>
        Assert.Equal(expected, ServiceDurations.FromName(name));
}
