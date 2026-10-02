using Microsoft.EntityFrameworkCore;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Bookings.FindNextFreeStart;

/// <summary>
/// First future slot without a non-cancelled appointment of the business, from the preferred slot on
/// <see cref="FindNextFreeStartQuery.Day"/> through the next two weeks. Sundays are skipped.
/// </summary>
public class FindNextFreeStartHandler
{
    public const int SearchDays = 14;

    private readonly AppDbContext _db;

    public FindNextFreeStartHandler(AppDbContext db) => _db = db;

    /// <summary>Null when every slot in the window is past or taken.</summary>
    public async Task<FreeStart?> HandleAsync(FindNextFreeStartQuery query, CancellationToken ct = default)
    {
        var firstDay = query.Day.Date;
        var from = AppTimeZones.LocalDateAndTimeToUtc(firstDay, TimeSpan.Zero);
        var to = AppTimeZones.LocalDateAndTimeToUtc(firstDay.AddDays(SearchDays), TimeSpan.Zero);
        var taken = (await _db.Appointments.AsNoTracking()
                .Where(a => a.GroomerId == query.BusinessId
                            && a.Status != AppointmentStatus.Cancelled
                            && a.ScheduledAt >= from
                            && a.ScheduledAt < to)
                .Select(a => a.ScheduledAt)
                .ToListAsync(ct))
            .ToHashSet();

        var preferredIndex = query.Slots
            .Select((slot, index) => (slot, index))
            .FirstOrDefault(x => string.Equals(x.slot, query.PreferredSlot, StringComparison.OrdinalIgnoreCase))
            .index;

        for (var dayOffset = 0; dayOffset < SearchDays; dayOffset++)
        {
            var day = firstDay.AddDays(dayOffset);
            if (day.DayOfWeek == DayOfWeek.Sunday) continue;

            var candidates = dayOffset == 0 ? query.Slots.Skip(preferredIndex) : query.Slots;
            foreach (var slot in candidates)
            {
                if (!AppTimeZones.TryParseSlotToTimeSpan(slot, out var tod)) continue;
                var startUtc = AppTimeZones.LocalDateAndTimeToUtc(day, tod);
                if (startUtc <= query.NowUtc || taken.Contains(startUtc)) continue;
                return new FreeStart(startUtc, day, slot);
            }
        }

        return null;
    }
}
