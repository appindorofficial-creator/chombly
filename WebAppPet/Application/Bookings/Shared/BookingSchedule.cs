using System.Globalization;
using WebAppPet.Domain.Markets;
using WebAppPet.Localization;

namespace WebAppPet.Application.Bookings.Shared;

/// <summary>Shared date helpers for Walkers / Daycare / Trainers booking flows.</summary>
public static class BookingDate
{
    public static bool TryParseSelected(string? date, out DateTime day)
    {
        day = default;
        if (string.IsNullOrWhiteSpace(date) || !DateTime.TryParse(date, out var parsed))
            return false;
        var today = AppTimeZones.TodayLocalDate();
        if (parsed.Date < today) return false;
        day = parsed.Date;
        return true;
    }

    public static string FormatLabel(DateTime day)
    {
        var today = AppTimeZones.TodayLocalDate();
        var datePart = day.ToString("d MMM yyyy");
        if (day.Date == today)
            return $"{CatalogLocalizer.Loc("Hoy", "Today")}, {datePart}";
        if (day.Date == today.AddDays(1))
            return $"{CatalogLocalizer.Loc("Mañana", "Tomorrow")}, {datePart}";
        return day.ToString("ddd d MMM yyyy");
    }

    /// <summary>
    /// Maps legacy When=hoy|manana|fecha + Date into a single Date value (yyyy-MM-dd).
    /// Returns cleared When and normalized Date.
    /// </summary>
    public static (string When, string? Date) NormalizeFromLegacy(string? when, string? date)
    {
        var today = AppTimeZones.TodayLocalDate();
        var key = (when ?? "").Trim().ToLowerInvariant();

        if (key is "hoy" or "today")
            return ("", today.ToString("yyyy-MM-dd"));

        if (key is "manana" or "mañana" or "tomorrow")
            return ("", today.AddDays(1).ToString("yyyy-MM-dd"));

        // Past dates (typed or soft-nav) clamp to today — never keep a past value in the flow.
        if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var parsed))
        {
            var day = parsed.Date < today ? today : parsed.Date;
            return ("", day.ToString("yyyy-MM-dd"));
        }

        return ("", null);
    }
}

/// <summary>Shared wall-clock slot helpers for Trainers / Walkers / Behavior / Booking / Vet.</summary>
public static class BookingTime
{
    public static readonly string[] DefaultSlots =
    {
        "9:00 AM", "10:00 AM", "11:00 AM", "1:00 PM", "2:00 PM", "3:00 PM", "4:00 PM", "5:00 PM"
    };

    /// <summary>Hourly chips for a regular business day when no opening hours constrain it.</summary>
    public static readonly string[] StandardDaySlots =
    {
        "9:00 AM", "10:00 AM", "11:00 AM", "12:00 PM", "1:00 PM", "2:00 PM", "3:00 PM", "4:00 PM", "5:00 PM"
    };

    /// <summary>
    /// Hourly start times from opening until the last start before closing.
    /// 24-hour days (open == close) and windows crossing midnight keep <see cref="StandardDaySlots"/>.
    /// </summary>
    public static List<string> OpenWindowSlots(int openMinutes, int closeMinutes)
    {
        openMinutes = Math.Clamp(openMinutes, 0, 24 * 60);
        closeMinutes = Math.Clamp(closeMinutes, 0, 24 * 60);
        if (closeMinutes <= openMinutes)
            return StandardDaySlots.ToList();

        var slots = new List<string>();
        for (var minutes = openMinutes; minutes < closeMinutes; minutes += 60)
            slots.Add(DateTime.MinValue.AddMinutes(minutes).ToString("h:mm tt", CultureInfo.InvariantCulture));
        return slots;
    }

    /// <summary>The slot label with the same time of day as <paramref name="time"/> (e.g. "17:00" → "5:00 PM").</summary>
    public static string? MatchSlot(string? time, IEnumerable<string> slots)
    {
        if (!AppTimeZones.TryParseSlotToTimeSpan(time, out var wanted)) return null;
        return slots.FirstOrDefault(s => AppTimeZones.TryParseSlotToTimeSpan(s, out var tod) && tod == wanted);
    }

    public static HashSet<string> MarkPastSlots(IEnumerable<string> slots, DateTime day, BusinessMarket? market = null)
    {
        var zone = market ?? AppTimeZones.CurrentMarket;
        var past = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nowUtc = DateTime.UtcNow;
        foreach (var label in slots)
        {
            if (!AppTimeZones.TryParseSlotToTimeSpan(label, out var tod)) continue;
            var utc = AppTimeZones.LocalDateAndTimeToUtc(day.Date, tod, zone);
            if (utc <= nowUtc)
                past.Add(label);
        }
        return past;
    }

    public static bool IsSlotAvailable(
        string? slot,
        IEnumerable<string> catalog,
        IReadOnlySet<string> past,
        IReadOnlySet<string> occupied)
    {
        if (string.IsNullOrWhiteSpace(slot)) return false;
        if (!catalog.Contains(slot, StringComparer.OrdinalIgnoreCase)) return false;
        if (past.Contains(slot)) return false;
        if (occupied.Contains(slot)) return false;
        return true;
    }

    /// <summary>Maps legacy walker keys (ahora/manana9/tarde/noche) into DefaultSlots labels.</summary>
    public static string? MapLegacySlot(string? slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) return null;
        var key = slot.Trim();
        if (DefaultSlots.Contains(key, StringComparer.OrdinalIgnoreCase))
            return DefaultSlots.First(s => s.Equals(key, StringComparison.OrdinalIgnoreCase));

        return key.ToLowerInvariant() switch
        {
            "manana9" => "9:00 AM",
            "tarde" => "1:00 PM",
            "noche" => "5:00 PM",
            "ahora" => null,
            _ => null
        };
    }

    public static bool TryResolveStartUtc(string? slot, DateTime day, out DateTime startUtc, out string? error)
    {
        error = null;
        startUtc = default;
        if (!AppTimeZones.TryParseSlotToTimeSpan(slot, out var tod))
        {
            error = CatalogLocalizer.Loc("Elige un horario.", "Choose a time slot.");
            return false;
        }

        startUtc = AppTimeZones.LocalDateAndTimeToUtc(day.Date, tod);
        if (startUtc <= DateTime.UtcNow)
        {
            error = CatalogLocalizer.Loc(
                "No puedes elegir una fecha u hora en el pasado.",
                "You can't select a past date or time.");
            return false;
        }

        return true;
    }
}
