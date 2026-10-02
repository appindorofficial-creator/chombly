using Microsoft.EntityFrameworkCore;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Bookings.GetBookingConfirmation;

/// <param name="Payment">Latest charge that didn't fail; null when nothing was charged.</param>
public sealed record BookingConfirmation(Appointment Appointment, PaymentTransaction? Payment);

public class GetBookingConfirmationHandler
{
    private readonly AppDbContext _db;

    public GetBookingConfirmationHandler(AppDbContext db) => _db = db;

    /// <summary>Null when the appointment doesn't exist or belongs to another client.</summary>
    public async Task<BookingConfirmation?> HandleAsync(GetBookingConfirmationQuery query, CancellationToken ct = default)
    {
        var appointment = await _db.Appointments.AsNoTracking()
            .Include(a => a.Groomer).ThenInclude(g => g.Category)
            .Include(a => a.Service)
            .Include(a => a.Pet)
            .Include(a => a.Extras)
            .FirstOrDefaultAsync(a => a.Id == query.AppointmentId && a.ClientId == query.ClientId, ct);
        if (appointment is null)
            return null;

        var payment = await _db.PaymentTransactions.AsNoTracking()
            .Where(t => t.AppointmentId == appointment.Id && t.Status != PaymentTransactionStatus.Failed)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return new BookingConfirmation(appointment, payment);
    }
}
