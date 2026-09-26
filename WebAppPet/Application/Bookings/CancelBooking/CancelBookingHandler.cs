using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Common;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;

namespace WebAppPet.Application.Bookings.CancelBooking;

public class CancelBookingHandler
{
    private readonly AppDbContext _db;
    private readonly PaymentService _payments;

    public CancelBookingHandler(AppDbContext db, PaymentService payments)
    {
        _db = db;
        _payments = payments;
    }

    /// <summary>Clients can cancel pending or confirmed appointments; the deposit is refunded in full.</summary>
    public async Task<Result> HandleAsync(CancelBookingCommand command, CancellationToken ct = default)
    {
        var appt = await _db.Appointments
            .Include(a => a.Groomer)
            .FirstOrDefaultAsync(a => a.Id == command.AppointmentId && a.ClientId == command.ClientId, ct);

        if (appt is null || appt.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed))
            return Result.Fail(CatalogLocalizer.Loc("Esta cita ya no se puede cancelar.", "This appointment can no longer be cancelled."));

        appt.Status = AppointmentStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
        var refunded = await _payments.RefundAppointmentAsync(appt.Id, "client_cancelled", command.ClientId, ct);

        _db.Notifications.Add(new AppNotification
        {
            UserId = command.ClientId,
            Title = CatalogLocalizer.Loc("Cita cancelada", "Appointment cancelled"),
            Message = refunded > 0
                ? CatalogLocalizer.Loc(
                    $"Cancelaste tu cita en {appt.Groomer.BusinessName}. Te reembolsamos el anticipo.",
                    $"You cancelled your appointment at {appt.Groomer.BusinessName}. Your deposit was refunded.")
                : CatalogLocalizer.Loc(
                    $"Cancelaste tu cita en {appt.Groomer.BusinessName}.",
                    $"You cancelled your appointment at {appt.Groomer.BusinessName}."),
            Type = "appointment"
        });
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
