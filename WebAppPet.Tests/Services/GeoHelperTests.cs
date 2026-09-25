using WebAppPet.Services;

namespace WebAppPet.Tests.Services;

public class GeoHelperTests
{
    [Theory]
    [InlineData(4711000, -74072100, 4.711, -74.0721)]        // Bogotá, "4.711000" read as thousands
    [InlineData(6244200, -75581200, 6.2442, -75.5812)]       // Medellín
    [InlineData(40712800, -74006000, 40.7128, -74.006)]      // New York
    [InlineData(28861202, -81234567, 28.861202, -81.234567)]
    public void Repairs_both_coordinates_with_one_shared_scale(double lat, double lng, double expectedLat, double expectedLng)
    {
        Assert.True(GeoHelper.TryRepairCoordinates(ref lat, ref lng));

        Assert.Equal(expectedLat, lat, 6);
        Assert.Equal(expectedLng, lng, 6);
    }

    [Fact]
    public void Repairs_a_single_mangled_latitude_with_the_places_scale_first()
    {
        double lat = 4711000, lng = -74.0721;

        Assert.True(GeoHelper.TryRepairCoordinates(ref lat, ref lng));

        Assert.Equal(4.711, lat, 6);
        Assert.Equal(-74.0721, lng, 6);
    }

    [Fact]
    public void Leaves_valid_coordinates_alone()
    {
        double lat = 4.711, lng = -74.0721;

        Assert.False(GeoHelper.TryRepairCoordinates(ref lat, ref lng));

        Assert.Equal(4.711, lat);
        Assert.Equal(-74.0721, lng);
    }
}
