using System.Globalization;

namespace WebAppPet.Services;

/// <summary>
/// Converts UTC timestamps (stored as datetime2 / Kind Unspecified) to the app market zone
/// (Charlotte, NC → Eastern) for display.
/// </summary>
public static class AppTimeZones
{
    private static readonly TimeZoneInfo Eastern = ResolveEastern();

    private static TimeZoneInfo ResolveEastern()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
    }

    /// <summary>
    /// Treats Unspecified values as UTC (EF + SQL Server datetime2), then converts to Eastern.
    /// </summary>
    public static DateTime ToAppLocal(DateTime utcOrUnspecified)
    {
        var utc = utcOrUnspecified.Kind switch
        {
            DateTimeKind.Utc => utcOrUnspecified,
            DateTimeKind.Local => utcOrUnspecified.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utcOrUnspecified, DateTimeKind.Utc)
        };
        return TimeZoneInfo.ConvertTimeFromUtc(utc, Eastern);
    }

    public static string FormatShort(DateTime utcOrUnspecified, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        return ToAppLocal(utcOrUnspecified).ToString("g", culture);
    }

    public static string FormatDate(DateTime utcOrUnspecified, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        return ToAppLocal(utcOrUnspecified).ToString("d", culture);
    }

    public static DateTime NowLocal() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Eastern);

    public static DateTime TodayLocalDate() => NowLocal().Date;

    /// <summary>Builds a UTC instant from a local calendar day + wall-clock time in Eastern.</summary>
    public static DateTime LocalDateAndTimeToUtc(DateTime localDate, TimeSpan localTimeOfDay)
    {
        var local = DateTime.SpecifyKind(localDate.Date.Add(localTimeOfDay), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, Eastern);
    }

    public static bool TryParseSlotToTimeSpan(string? slot, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(slot)) return false;
        if (DateTime.TryParse(slot, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ||
            DateTime.TryParse(slot, CultureInfo.CurrentCulture, DateTimeStyles.None, out dt))
        {
            time = dt.TimeOfDay;
            return true;
        }
        return false;
    }
}
