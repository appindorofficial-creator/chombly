using System.Globalization;

namespace WebAppPet.Domain.Markets;

/// <summary>
/// Converts UTC timestamps (stored as datetime2 / Kind Unspecified) to the active market zone:
/// Colombia → America/Bogota (UTC−5), United States → America/New_York (Eastern).
/// The active market is the one forced with <see cref="UseMarket"/>, else the signed-in user's
/// (set for the whole request with <see cref="UseRequest"/>), else Colombia.
/// </summary>
public static class AppTimeZones
{
    public const string BogotaId = "America/Bogota";
    public const string NewYorkId = "America/New_York";

    private static readonly TimeZoneInfo Bogota = ResolveZone(BogotaId, "SA Pacific Standard Time");
    private static readonly TimeZoneInfo NewYork = ResolveZone(NewYorkId, "Eastern Standard Time");

    private static readonly AsyncLocal<BusinessMarket?> OverrideMarket = new();
    private static readonly AsyncLocal<RequestMarket?> Request = new();

    private sealed record RequestMarket(BusinessMarket Market, string CountryIso);

    public static string TimeZoneId => ZoneFor(CurrentMarket).Id;

    public static BusinessMarket CurrentMarket
    {
        get
        {
            if (OverrideMarket.Value is BusinessMarket o)
                return o;

            if (Request.Value is { Market: var m } && m != BusinessMarket.Unknown)
                return m;

            return BusinessMarket.Colombia;
        }
    }

    /// <summary>The signed-in user's market and home country for the rest of the request.</summary>
    public static IDisposable UseRequest(BusinessMarket market, string countryIso)
    {
        var previous = Request.Value;
        Request.Value = new RequestMarket(market, countryIso);
        return new OverrideScope(() => Request.Value = previous);
    }

    /// <summary>
    /// Temporarily use a market (e.g. consultation ContextCountry, a subscription's currency);
    /// <see cref="CurrentCountryCode"/> follows it.
    /// </summary>
    public static IDisposable UseMarket(BusinessMarket market)
    {
        var previous = OverrideMarket.Value;
        OverrideMarket.Value = market == BusinessMarket.Unknown ? null : market;
        return new OverrideScope(() => OverrideMarket.Value = previous);
    }

    public static BusinessMarket MarketFromCountry(string? countryIso)
    {
        var fromIso = BusinessMarketResolver.FromIso(countryIso);
        return fromIso == BusinessMarket.Unknown ? CurrentMarket : fromIso;
    }

    /// <summary>ISO home country for this request (from persisted user flag).</summary>
    public static string CurrentCountryCode
    {
        get
        {
            if (OverrideMarket.Value is BusinessMarket forced)
                return MarketCountry.FromMarket(forced);
            if (Request.Value is { CountryIso: var iso } && !string.IsNullOrWhiteSpace(iso))
                return MarketCountry.Normalize(iso);
            return MarketCountry.FromMarket(CurrentMarket);
        }
    }

    public static TimeZoneInfo ZoneFor(BusinessMarket market) =>
        market == BusinessMarket.UnitedStates ? NewYork : Bogota;

    private static TimeZoneInfo CurrentZone => ZoneFor(CurrentMarket);

    private static TimeZoneInfo ResolveZone(string iana, string windowsFallback)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(iana);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsFallback);
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsFallback);
        }
    }

    /// <summary>
    /// Treats Unspecified values as UTC (EF + SQL Server datetime2), then converts to the market zone.
    /// </summary>
    public static DateTime ToAppLocal(DateTime utcOrUnspecified) =>
        ToAppLocal(utcOrUnspecified, CurrentMarket);

    public static DateTime ToAppLocal(DateTime utcOrUnspecified, BusinessMarket market)
    {
        var utc = utcOrUnspecified.Kind switch
        {
            DateTimeKind.Utc => utcOrUnspecified,
            DateTimeKind.Local => utcOrUnspecified.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utcOrUnspecified, DateTimeKind.Utc)
        };
        return TimeZoneInfo.ConvertTimeFromUtc(utc, ZoneFor(market));
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

    public static DateTime NowLocal() => NowLocal(CurrentMarket);

    public static DateTime NowLocal(BusinessMarket market) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ZoneFor(market));

    public static DateTime TodayLocalDate() => TodayLocalDate(CurrentMarket);

    public static DateTime TodayLocalDate(BusinessMarket market) => NowLocal(market).Date;

    /// <summary>Builds a UTC instant from a local calendar day + wall-clock time in the market zone.</summary>
    public static DateTime LocalDateAndTimeToUtc(DateTime localDate, TimeSpan localTimeOfDay) =>
        LocalDateAndTimeToUtc(localDate, localTimeOfDay, CurrentMarket);

    public static DateTime LocalDateAndTimeToUtc(DateTime localDate, TimeSpan localTimeOfDay, BusinessMarket market)
    {
        var local = DateTime.SpecifyKind(localDate.Date.Add(localTimeOfDay), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, ZoneFor(market));
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

    private sealed class OverrideScope : IDisposable
    {
        private readonly Action _restore;
        private bool _disposed;

        public OverrideScope(Action restore) => _restore = restore;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _restore();
        }
    }
}
