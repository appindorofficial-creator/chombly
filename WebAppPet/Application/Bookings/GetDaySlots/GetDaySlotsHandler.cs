using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Bookings.Shared;
using WebAppPet.Application.Businesses.Shared;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Bookings.GetDaySlots;

public class GetDaySlotsHandler
{
    private readonly AppDbContext _db;
    private readonly AvailabilityService _availability;

    public GetDaySlotsHandler(AppDbContext db, AvailabilityService availability)
    {
        _db = db;
        _availability = availability;
    }

    public async Task<DaySlots> HandleAsync(GetDaySlotsQuery query, CancellationToken ct = default)
    {
        var day = query.Day.Date;
        var isOpen = query.OpenAllDay || await _availability.IsAvailableOnAsync(query.BusinessId, day);

        BusinessWeeklyHour? week = null;
        string? hoursLabel = null;
        if (!query.OpenAllDay)
        {
            week = await _db.WeeklyHours.AsNoTracking()
                .FirstOrDefaultAsync(h => h.GroomerId == query.BusinessId && h.DayOfWeek == (int)day.DayOfWeek, ct);
            if (week is { IsOpen: true })
                hoursLabel = $"{week.OpenLabel}–{week.CloseLabel}";
        }

        var timeSlots = week is { IsOpen: true }
            ? BookingTime.OpenWindowSlots(week.OpenMinutes, week.CloseMinutes)
            : BookingTime.StandardDaySlots.ToList();

        var outsideHours = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bookable = new List<string>();
        if (!isOpen)
        {
            return new DaySlots(false, hoursLabel, timeSlots,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                outsideHours, bookable);
        }

        var past = BookingTime.MarkPastSlots(timeSlots, day);
        var occupied = await OccupiedSlotsAsync(query.BusinessId, day, timeSlots, ct);

        foreach (var label in timeSlots)
        {
            if (!AppTimeZones.TryParseSlotToTimeSpan(label, out var tod)) continue;
            var minutes = (int)tod.TotalMinutes;

            if (week is { IsOpen: true }
                && !AvailabilityService.IsWithinOpenWindow(minutes, week.OpenMinutes, week.CloseMinutes))
            {
                outsideHours.Add(label);
                continue;
            }

            if (past.Contains(label) || occupied.Contains(label))
                continue;

            bookable.Add(label);
        }

        return new DaySlots(true, hoursLabel, timeSlots, past, occupied, outsideHours, bookable);
    }

    private async Task<HashSet<string>> OccupiedSlotsAsync(int businessId, DateTime day, List<string> timeSlots, CancellationToken ct)
    {
        var occupied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var from = AppTimeZones.LocalDateAndTimeToUtc(day, TimeSpan.Zero);
        var to = AppTimeZones.LocalDateAndTimeToUtc(day.AddDays(1), TimeSpan.Zero);
        var taken = await _db.Appointments.AsNoTracking()
            .Where(a => a.GroomerId == businessId
                        && a.Status != AppointmentStatus.Cancelled
                        && a.ScheduledAt >= from
                        && a.ScheduledAt < to)
            .Select(a => a.ScheduledAt)
            .ToListAsync(ct);

        foreach (var utc in taken)
        {
            var local = AppTimeZones.ToAppLocal(utc);
            foreach (var label in timeSlots)
            {
                if (AppTimeZones.TryParseSlotToTimeSpan(label, out var slotTod) && slotTod == local.TimeOfDay)
                    occupied.Add(label);
            }
        }

        return occupied;
    }
}
