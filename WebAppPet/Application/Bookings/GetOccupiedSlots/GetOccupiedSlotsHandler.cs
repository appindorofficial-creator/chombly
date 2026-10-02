using Microsoft.EntityFrameworkCore;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Bookings.GetOccupiedSlots;

public class GetOccupiedSlotsHandler
{
    private readonly AppDbContext _db;

    public GetOccupiedSlotsHandler(AppDbContext db) => _db = db;

    /// <summary>Slots that start exactly when a non-cancelled appointment of the business does.</summary>
    public async Task<HashSet<string>> HandleAsync(GetOccupiedSlotsQuery query, CancellationToken ct = default)
    {
        var day = query.Day.Date;
        var from = AppTimeZones.LocalDateAndTimeToUtc(day, TimeSpan.Zero);
        var to = AppTimeZones.LocalDateAndTimeToUtc(day.AddDays(1), TimeSpan.Zero);
        var taken = await _db.Appointments.AsNoTracking()
            .Where(a => a.GroomerId == query.BusinessId
                        && a.Status != AppointmentStatus.Cancelled
                        && a.ScheduledAt >= from
                        && a.ScheduledAt < to)
            .Select(a => a.ScheduledAt)
            .ToListAsync(ct);

        var occupied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var utc in taken)
        {
            var local = AppTimeZones.ToAppLocal(utc);
            foreach (var label in query.Slots)
            {
                if (AppTimeZones.TryParseSlotToTimeSpan(label, out var slotTod) && slotTod == local.TimeOfDay)
                    occupied.Add(label);
            }
        }

        return occupied;
    }
}
