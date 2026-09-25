using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Application.Bookings.GetBookings;

public class GetBusinessBookingsHandler
{
    private readonly AppDbContext _db;

    public GetBusinessBookingsHandler(AppDbContext db) => _db = db;

    /// <summary>Requests: pending. Upcoming: confirmed. History: completed or cancelled, newest first.</summary>
    public async Task<List<Appointment>> HandleAsync(GetBusinessBookingsQuery query, CancellationToken ct = default)
    {
        var appointments = _db.Appointments.AsNoTracking()
            .Include(a => a.Pet)
            .Include(a => a.Service)
            .Include(a => a.Client)
            .Where(a => a.GroomerId == query.BusinessId);

        return query.Tab switch
        {
            BusinessBookingsTab.Upcoming => await appointments
                .Where(a => a.Status == AppointmentStatus.Confirmed)
                .OrderBy(a => a.ScheduledAt)
                .ToListAsync(ct),
            BusinessBookingsTab.History => await appointments
                .Where(a => a.Status == AppointmentStatus.Completed || a.Status == AppointmentStatus.Cancelled)
                .OrderByDescending(a => a.ScheduledAt)
                .ToListAsync(ct),
            _ => await appointments
                .Where(a => a.Status == AppointmentStatus.Pending)
                .OrderBy(a => a.ScheduledAt)
                .ToListAsync(ct)
        };
    }
}
