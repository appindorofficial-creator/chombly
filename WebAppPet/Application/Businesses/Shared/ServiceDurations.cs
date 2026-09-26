using System.Text.RegularExpressions;

namespace WebAppPet.Application.Businesses.Shared;

public static partial class ServiceDurations
{
    public const int NightMinutes = 1440;
    public const int DefaultMinutes = 60;

    /// <summary>"Paseo 30 min" and "30-min walk" last 30 minutes; names without minutes last an hour.</summary>
    public static int FromName(string? name)
    {
        var match = MinutesInName().Match(name ?? "");
        return match.Success
               && int.TryParse(match.Groups[1].Value, out var minutes)
               && minutes is > 0 and < NightMinutes
            ? minutes
            : DefaultMinutes;
    }

    [GeneratedRegex(@"(\d{1,4})\s*-?\s*min", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MinutesInName();
}
