using WebAppPet.Application.Businesses.SearchBusinesses;
using WebAppPet.Models;

namespace WebAppPet.Tests.Application.Businesses;

public class BusinessListingTests
{
    private static GroomerProfile Business(string about = "", params string[] amenities) => new()
    {
        About = about,
        City = "Medellín",
        Amenities = amenities.Select(a => new BusinessAmenity { Label = a }).ToList()
    };

    [Fact]
    public void Amenity_match_looks_at_labels_and_about_ignoring_case()
    {
        Assert.True(BusinessListing.AmenityMatch(Business("", "Fotos del PASEO"), "foto"));
        Assert.True(BusinessListing.AmenityMatch(Business("Clases en el parque"), "gps", "parque"));
        Assert.False(BusinessListing.AmenityMatch(Business("Paseos", "Wifi"), "foto"));
    }

    [Fact]
    public void Distance_is_empty_without_user_or_business_coordinates()
    {
        var business = Business();
        business.Latitude = 6.2442;
        business.Longitude = -75.5812;

        Assert.Equal((null, null), BusinessListing.DistanceFrom(null, null, business));
        Assert.Equal((null, null), BusinessListing.DistanceFrom(4.711, -74.0721, Business()));
    }

    [Fact]
    public void Distance_label_shows_km_nearby_and_the_city_when_far()
    {
        var far = Business();
        far.Latitude = 6.2442;
        far.Longitude = -75.5812;
        var near = Business();
        near.Latitude = 4.6486;
        near.Longitude = -74.0628;

        var (farMiles, farLabel) = BusinessListing.DistanceFrom(4.711, -74.0721, far);
        var (nearMiles, nearLabel) = BusinessListing.DistanceFrom(4.711, -74.0721, near);

        Assert.InRange(farMiles!.Value, 140, 160);
        Assert.Equal("Medellín", farLabel);
        Assert.InRange(nearMiles!.Value, 4, 5);
        Assert.StartsWith("A ", nearLabel);
    }

    [Theory]
    [InlineData(5, false, 3, true)]
    [InlineData(5, true, 5, false)]
    [InlineData(3, false, 3, false)]
    public void First_page_shows_three_unless_all_are_requested(int count, bool showAll, int shown, bool hasMore)
    {
        var page = BusinessListing.FirstPage(Enumerable.Range(1, count).ToList(), showAll, out var more);

        Assert.Equal(shown, page.Count);
        Assert.Equal(hasMore, more);
    }
}
