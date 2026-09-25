using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Businesses.SearchBusinesses;

/// <summary>Rules shared by the public business listings.</summary>
public static class BusinessListing
{
    /// <summary>Cards shown before the "see more" link.</summary>
    public const int FirstPageSize = 3;

    public static bool AcceptsAll(GroomerProfile business, IEnumerable<string> species) =>
        species.All(business.AcceptsSpecies);

    /// <summary>True when any key appears in the amenity labels or the About text (case-insensitive).</summary>
    public static bool AmenityMatch(GroomerProfile business, params string[] keys)
    {
        var blob = string.Join(" ", business.Amenities.Select(a => a.Label)) + " " + (business.About ?? "");
        return keys.Any(k => blob.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Miles from the user plus the card label ("A 3,2 km de ti" nearby, the city when far).</summary>
    public static (double? Miles, string? Label) DistanceFrom(double? userLat, double? userLng, GroomerProfile business)
    {
        if (userLat is not double lat || userLng is not double lng) return (null, null);
        if (business.Latitude == 0 && business.Longitude == 0) return (null, null);

        var miles = GeoHelper.MilesBetween(lat, lng, business.Latitude, business.Longitude);
        var km = miles * 1.609344;
        return (miles, GeoHelper.FormatDistanceOrPlace(km, business.City, business.Address));
    }

    public static List<T> FirstPage<T>(List<T> items, bool showAll, out bool hasMore)
    {
        hasMore = !showAll && items.Count > FirstPageSize;
        return hasMore ? items.Take(FirstPageSize).ToList() : items;
    }
}
