using System.Globalization;

namespace WebAppPet.Application.Accounts.Shared;

public static class Coordinates
{
    /// <summary>Accepts both "4.6" and "4,6" (browser locale) and parses with the invariant culture.</summary>
    public static bool TryParse(string? value, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        value = value.Trim().Replace(',', '.');
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>In range and not (0, 0), which is what an empty map picker posts.</summary>
    public static bool IsValidPair(double lat, double lng) =>
        lat is >= -90 and <= 90 && lng is >= -180 and <= 180 && !(lat == 0 && lng == 0);

    public static bool TryParsePair(string? latitude, string? longitude, out double lat, out double lng)
    {
        lng = 0;
        return TryParse(latitude, out lat) && TryParse(longitude, out lng) && IsValidPair(lat, lng);
    }

    public static string? Format(double? value) => value?.ToString("0.######", CultureInfo.InvariantCulture);
}
