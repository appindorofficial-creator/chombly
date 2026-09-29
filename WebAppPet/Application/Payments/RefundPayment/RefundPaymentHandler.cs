using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Common;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Localization;

namespace WebAppPet.Application.Payments.RefundPayment;

/// <summary>
/// Admin refund of a single charge. What it paid for is released too: an upcoming appointment is cancelled
/// and a Care membership ends. The family is always told; the business too when its appointment is cancelled.
/// </summary>
public class RefundPaymentHandler
{
    private readonly AppDbContext _db;
    private readonly PaymentService _payments;

    public RefundPaymentHandler(AppDbContext db, PaymentService payments)
    {
        _db = db;
        _payments = payments;
    }

    public async Task<Result> HandleAsync(RefundPaymentCommand command, CancellationToken ct = default)
    {
        var transaction = await _db.PaymentTransactions.FirstOrDefaultAsync(t => t.Id == command.TransactionId, ct);
        if (transaction is null)
            return Result.Fail(CatalogLocalizer.Loc("No encontramos ese pago.", "Payment not found."));

        var reason = string.IsNullOrWhiteSpace(command.Reason) ? "admin_refund" : command.Reason.Trim();
        if (!await _payments.RefundAsync(transaction, reason, command.ActorUserId, ct))
            return Result.Fail(CatalogLocalizer.Loc(
                "Este pago no se puede reembolsar (fallido o ya reembolsado).",
                "This payment can't be refunded (failed or already refunded)."));

        await ReleaseAsync(transaction, ct);
        return Result.Ok();
    }

    private async Task ReleaseAsync(PaymentTransaction transaction, CancellationToken ct)
    {
        var amount = AppMoney.FormatCurrency(transaction.Amount, transaction.Currency);
        var appointment = transaction.AppointmentId is int appointmentId
            ? await _db.Appointments.Include(a => a.Groomer).FirstOrDefaultAsync(a => a.Id == appointmentId, ct)
            : null;
        var subscription = transaction.CareSubscriptionId is int subscriptionId
            ? await _db.CareSubscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId, ct)
            : null;

        if (appointment is not null)
        {
            var business = appointment.Groomer.BusinessName;
            var when = AppTimeZones.FormatShort(appointment.ScheduledAt);
            var cancel = appointment.Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed;
            if (cancel)
            {
                appointment.Status = AppointmentStatus.Cancelled;
                Notify(appointment.Groomer.UserId, "appointment",
                    "Cita cancelada",
                    $"Chombly canceló la cita del {when} y reembolsó el pago a la familia.");
            }

            Notify(transaction.UserId, "appointment",
                "Reembolso de tu cita",
                cancel
                    ? $"Cancelamos tu cita en {business} ({when}) y te reembolsamos {amount}."
                    : $"Te reembolsamos {amount} de tu cita en {business} ({when}).");
        }
        else if (subscription is not null)
        {
            subscription.Status = CareSubscriptionStatus.Cancelled;
            Notify(transaction.UserId, "care",
                "Chombly Care cancelado",
                $"Te reembolsamos {amount} y tu membresía Care terminó.");
        }
        else
        {
            Notify(transaction.UserId, "info", "Reembolso", $"Te reembolsamos {amount}.");
        }

        await _db.SaveChangesAsync(ct);
    }

    private void Notify(int userId, string type, string title, string message) =>
        _db.Notifications.Add(new AppNotification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            CreatedAt = DateTime.UtcNow
        });
}
