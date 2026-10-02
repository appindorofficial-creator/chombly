namespace WebAppPet.Application.Bookings.GetDaySlots;

/// <param name="HoursLabel">Opening window from the weekly hours (e.g. "09:00–18:00"); null without one.</param>
/// <param name="TimeSlots">Every chip shown for the day.</param>
/// <param name="Bookable">Chips that are inside opening hours, not past and not taken. Empty when the day is closed.</param>
public sealed record DaySlots(
    bool IsOpen,
    string? HoursLabel,
    List<string> TimeSlots,
    HashSet<string> Past,
    HashSet<string> Occupied,
    HashSet<string> OutsideHours,
    List<string> Bookable);
