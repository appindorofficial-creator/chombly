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
