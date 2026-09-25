using WebAppPet.Application.Accounts.Shared;

namespace WebAppPet.Tests.Application.Accounts;

public class CoordinatesTests
{
    [Theory]
    [InlineData("4.711", 4.711)]
    [InlineData(" 4,711 ", 4.711)]
    [InlineData("-74.0721", -74.0721)]
    public void TryParse_accepts_dot_or_comma(string value, double expected)
    {
        Assert.True(Coordinates.TryParse(value, out var result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public void TryParse_rejects_blank_or_text(string? value) =>
        Assert.False(Coordinates.TryParse(value, out _));

    [Theory]
    [InlineData("4.711", "-74.0721", true)]
    [InlineData("0", "0", false)]
    [InlineData("91", "10", false)]
    [InlineData("10", "-181", false)]
    [InlineData("10", null, false)]
    public void TryParsePair_requires_both_values_in_range_and_not_origin(string? lat, string? lng, bool expected) =>
        Assert.Equal(expected, Coordinates.TryParsePair(lat, lng, out _, out _));

    [Fact]
    public void Format_uses_invariant_culture_and_trims_zeros()
    {
        Assert.Equal("4.711", Coordinates.Format(4.7110000));
        Assert.Null(Coordinates.Format(null));
    }
}
