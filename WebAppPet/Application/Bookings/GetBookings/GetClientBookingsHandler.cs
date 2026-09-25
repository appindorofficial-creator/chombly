using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Application.Bookings.GetBookings;

public class GetClientBookingsHandler
{
    private readonly AppDbContext _db;

    public GetClientBookingsHandler(AppDbContext db) => _db = db;

    /// <summary>Upcoming: pending or confirmed from today on. History: finished, cancelled or already past.</summary>
    public async Task<List<Appointment>> HandleAsync(GetClientBookingsQuery query, CancellationToken ct = default)
    {
        var appointments = _db.Appointments.AsNoTracking()
            .Include(a => a.Groomer).ThenInclude(g => g.Category)
            .Include(a => a.Service)
            .Include(a => a.Pet)
            .Where(a => a.ClientId == query.ClientId);

        if (query.History)
        {
            return await appointments
                .Where(a => a.Status == AppointmentStatus.Completed
                            || a.Status == AppointmentStatus.Cancelled
                            || a.ScheduledAt < query.NowUtc)
                .OrderByDescending(a => a.ScheduledAt)
                .ToListAsync(ct);
        }

        return await appointments
            .Where(a => a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed)
            .Where(a => a.ScheduledAt >= query.TodayStartUtc)
            .OrderBy(a => a.ScheduledAt)
            .ToListAsync(ct);
    }
}
