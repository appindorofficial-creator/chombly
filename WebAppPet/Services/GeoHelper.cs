namespace WebAppPet.Services;

public static class GeoHelper
{
    /// <summary>Distancia en millas. Null si faltan coordenadas.</summary>
    public static double? MilesBetween(double lat1, double lon1, double lat2, double lon2)
    {
        if (lat1 == 0 && lon1 == 0) return null;
        if (lat2 == 0 && lon2 == 0) return null;

        const double R = 3958.8; // Earth miles
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    public static string? FormatMilesAway(double? miles) =>
        miles is null ? null : $"A {miles.Value:0.0} mi de ti";

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
}
