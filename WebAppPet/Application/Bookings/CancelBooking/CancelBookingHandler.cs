using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Common;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;

namespace WebAppPet.Application.Bookings.CancelBooking;

public class CancelBookingHandler
{
    private readonly AppDbContext _db;

    public CancelBookingHandler(AppDbContext db) => _db = db;

    /// <summary>Clients can cancel pending or confirmed appointments.</summary>
    public async Task<Result> HandleAsync(CancelBookingCommand command, CancellationToken ct = default)
    {
        var appt = await _db.Appointments
            .Include(a => a.Groomer)
            .FirstOrDefaultAsync(a => a.Id == command.AppointmentId && a.ClientId == command.ClientId, ct);

        if (appt is null || appt.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed))
            return Result.Fail(CatalogLocalizer.Loc("Esta cita ya no se puede cancelar.", "This appointment can no longer be cancelled."));

        appt.Status = AppointmentStatus.Cancelled;
        _db.Notifications.Add(new AppNotification
        {
            UserId = command.ClientId,
            Title = CatalogLocalizer.Loc("Cita cancelada", "Appointment cancelled"),
            Message = CatalogLocalizer.Loc(
                $"Cancelaste tu cita en {appt.Groomer.BusinessName}.",
                $"You cancelled your appointment at {appt.Groomer.BusinessName}."),
            Type = "appointment"
        });
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
