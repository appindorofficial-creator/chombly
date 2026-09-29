using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Bookings.Shared;
using WebAppPet.Application.Common;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Localization;

namespace WebAppPet.Application.Bookings.UpdateBookingStatus;

/// <summary>Business-side transitions: accept or reject a pending request, complete a confirmed visit.</summary>
public class UpdateBookingStatusHandler
{
    private readonly AppDbContext _db;
    private readonly ClinicalNotes _clinicalNotes;
    private readonly PaymentService _payments;

    public UpdateBookingStatusHandler(AppDbContext db, ClinicalNotes clinicalNotes, PaymentService payments)
    {
        _db = db;
        _clinicalNotes = clinicalNotes;
        _payments = payments;
    }

    public async Task<Result> HandleAsync(UpdateBookingStatusCommand command, CancellationToken ct = default)
    {
        var (from, to, title, messageSuffix) = Transition(command.Action);

        var appt = await _db.Appointments
            .Include(a => a.Groomer)
            .FirstOrDefaultAsync(a => a.Id == command.AppointmentId && a.GroomerId == command.BusinessId, ct);

        if (appt is null || appt.Status != from)
            return Result.Fail(CatalogLocalizer.Loc("La cita cambió de estado. Actualiza la página.", "The appointment status changed. Refresh the page."));

        appt.Status = to;
        var note = command.ClinicalNotes?.Trim();
        if (!string.IsNullOrWhiteSpace(note))
            await _clinicalNotes.ApplyAsync(appt, appt.Groomer.BusinessName, note, ct);
        else if (to == AppointmentStatus.Completed)
            await _clinicalNotes.MarkConsultationCompletedAsync(appt, ct);

        var refunded = 0;
        if (to == AppointmentStatus.Cancelled)
        {
            await _db.SaveChangesAsync(ct);
            refunded = await _payments.RefundAppointmentAsync(appt.Id, "business_rejected", appt.Groomer.UserId, ct);
        }

        var refundNote = refunded > 0 ? " Te reembolsamos el anticipo." : "";
        _db.Notifications.Add(new AppNotification
        {
            UserId = appt.ClientId,
            Title = title,
            Message = $"{appt.Groomer.BusinessName} {messageSuffix} ({AppTimeZones.FormatShort(appt.ScheduledAt)}).{refundNote}",
            Type = "appointment"
        });
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static (AppointmentStatus From, AppointmentStatus To, string Title, string MessageSuffix) Transition(
        BookingStatusAction action) => action switch
    {
        BookingStatusAction.Accept => (AppointmentStatus.Pending, AppointmentStatus.Confirmed,
            "¡Cita confirmada!", "confirmó tu cita"),
        BookingStatusAction.Reject => (AppointmentStatus.Pending, AppointmentStatus.Cancelled,
            "Cita rechazada", "no pudo aceptar tu cita"),
        BookingStatusAction.Complete => (AppointmentStatus.Confirmed, AppointmentStatus.Completed,
            "Servicio completado", "completó el servicio. ¡Cuéntanos cómo quedó tu mascota!"),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
    };
}
