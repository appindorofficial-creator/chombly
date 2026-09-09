namespace WebAppPet.Services;

public static class GeoHelper
{
    /// <summary>Centros aproximados de los estados del selector de Vet Virtual.</summary>
    private static readonly (string Code, double Lat, double Lng)[] VetStateCenters =
    {
        ("NC", 35.54, -79.39),
        ("SC", 33.86, -80.90),
        ("VA", 37.52, -78.85),
        ("GA", 32.66, -83.44),
        ("TN", 35.86, -86.35),
        ("FL", 27.77, -81.69),
        ("NY", 42.95, -75.53),
        ("CA", 37.18, -119.47),
        ("TX", 31.17, -99.68),
    };

    private static readonly Dictionary<string, string> StateNameToCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NORTH CAROLINA"] = "NC", ["SOUTH CAROLINA"] = "SC", ["VIRGINIA"] = "VA",
        ["GEORGIA"] = "GA", ["TENNESSEE"] = "TN", ["FLORIDA"] = "FL",
        ["NEW YORK"] = "NY", ["CALIFORNIA"] = "CA", ["TEXAS"] = "TX",
    };

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

    /// <summary>
    /// Infiere un código de estado del selector Vet a partir de lat/lng del usuario.
    /// Fuera de EE.UU. continental o lejos de los estados listados → "Other".
    /// </summary>
    public static string? GuessUsStateCode(double? lat, double? lng)
    {
        if (lat is not double la || lng is not double lo) return null;
        if (la is < 24 or > 50 || lo is < -125 or > -66) return "Other";

        string? best = null;
        var bestMiles = double.MaxValue;
        foreach (var (code, cLat, cLng) in VetStateCenters)
        {
            var miles = MilesBetween(la, lo, cLat, cLng) ?? double.MaxValue;
            if (miles < bestMiles)
            {
                bestMiles = miles;
                best = code;
            }
        }

        // Más de ~450 mi del centro más cercano → no está en esos estados.
        if (best is null || bestMiles > 450) return "Other";
        return best;
    }

    /// <summary>Intenta leer un código de estado desde City (p. ej. "Charlotte, NC").</summary>
    public static string? GuessUsStateFromCity(string? city)
    {
        if (string.IsNullOrWhiteSpace(city)) return null;
        var t = city.Trim();
        var upper = t.ToUpperInvariant();

        foreach (var (name, code) in StateNameToCode)
        {
            if (upper.Contains(name, StringComparison.Ordinal)) return code;
        }

        foreach (var (code, _, _) in VetStateCenters)
        {
            if (upper.EndsWith(", " + code, StringComparison.Ordinal) ||
                upper.EndsWith(" " + code, StringComparison.Ordinal) ||
                upper.EndsWith("," + code, StringComparison.Ordinal))
                return code;
        }

        return null;
    }

    public static string? ResolveUsState(string? city, double? lat, double? lng) =>
        GuessUsStateFromCity(city) ?? GuessUsStateCode(lat, lng);

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
}
