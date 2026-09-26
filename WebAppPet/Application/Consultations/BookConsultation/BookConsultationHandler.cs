using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Consultations.GetConsultationCheckout;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.BookConsultation;

/// <summary>
/// Books the consultation as a pending appointment with its provider, charged to the card through
/// the payment gateway or covered by a Care quick consult. Saves the consents, notifies the client and logs the payment.
/// </summary>
public class BookConsultationHandler
{
    private readonly AppDbContext _db;
    private readonly GetConsultationCheckoutHandler _checkout;
    private readonly ChomblyCareService _care;
    private readonly ConsentService _consent;
    private readonly VetAuditService _audit;
    private readonly PaymentService _payments;

    public BookConsultationHandler(
        AppDbContext db,
        GetConsultationCheckoutHandler checkout,
        ChomblyCareService care,
        ConsentService consent,
        VetAuditService audit,
        PaymentService payments)
    {
        _db = db;
        _checkout = checkout;
        _care = care;
        _consent = consent;
        _audit = audit;
        _payments = payments;
    }

    public async Task<BookConsultationResult> HandleAsync(BookConsultationCommand command, CancellationToken ct = default)
    {
        var details = await _checkout.HandleAsync(new GetConsultationCheckoutQuery(command.ClientId, command.ConsultationId), ct);
        if (details is null)
            return new BookConsultationResult(BookConsultationOutcome.NotFound, null, false);

        var consultation = details.Consultation;
        var item = details.CatalogItem;
        if (!command.AcceptTerms || !command.AcceptScope
            || consultation.ProviderId is null || consultation.PetId is null || consultation.ScheduledAt is null)
            return new BookConsultationResult(BookConsultationOutcome.Incomplete, details, false);

        var usingCare = command.WantsCare && details.HasCareAvailable;
        var charge = usingCare ? 0 : details.CatalogPrice;

        PaymentMethod? card = null;
        if (!usingCare)
        {
            card = command.PaymentMethodId is int cardId
                ? details.Payments.FirstOrDefault(p => p.Id == cardId)
                : details.DefaultPayment;
            if (card is null)
                return new BookConsultationResult(BookConsultationOutcome.NoPaymentMethod, details, false);
        }

        if (usingCare && !await _care.TryConsumeQuickConsultAsync(command.ClientId, consultation.Id, ct))
            return new BookConsultationResult(BookConsultationOutcome.CareBenefitFailed, details, true);

        PaymentTransaction? payment = null;
        if (!usingCare && charge > 0)
        {
            payment = await _payments.ChargeAsync(new ChargeRequest
            {
                UserId = command.ClientId,
                Card = card!,
                Amount = charge,
                Purpose = PaymentPurpose.VetConsultation,
                Description = $"Vet consultation #{consultation.Id} ({item.Code})",
                ProviderId = consultation.ProviderId,
                ConsultationId = consultation.Id
            }, ct);
            if (payment.Status != PaymentTransactionStatus.Succeeded)
                return new BookConsultationResult(BookConsultationOutcome.PaymentDeclined, details, false,
                    PaymentFailureCodes.Message(payment.FailureCode));
        }

        var service = await ProviderServiceAsync(consultation.ProviderId.Value, item, ct);
        var payNote = usingCare
            ? " | Chombly Care benefit"
            : $" | Card {card!.Brand} •••• {card.Last4}";

        var appointment = new Appointment
        {
            ClientId = command.ClientId,
            PetId = consultation.PetId.Value,
            GroomerId = consultation.ProviderId.Value,
            ServiceId = service.Id,
            ScheduledAt = consultation.ScheduledAt.Value,
            Status = AppointmentStatus.Pending,
            TotalPrice = charge,
            DepositPaid = charge,
            Notes = $"Vet consultation #{consultation.Id} ({item.Code}){payNote}. Symptoms: {consultation.Symptoms}",
            CreatedAt = DateTime.UtcNow
        };
        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync(ct);
        if (payment is not null)
            await _payments.AttachAppointmentAsync(payment, appointment.Id, ct);

        await _consent.SaveAsync(command.ClientId, consultation.Id, new[]
        {
            (ConsentService.DocTerms, command.AcceptTerms),
            (ConsentService.DocPrivacy, command.AcceptTerms),
            (ConsentService.DocIntlOrientation, command.AcceptScope && item.Code == ServiceCatalogCodes.VetIntl30),
            (ConsentService.DocMedia, command.AcceptMedia)
        }, command.IpAddress, command.UserAgent, ct);

        consultation.AppointmentId = appointment.Id;
        consultation.PriceCharged = charge;
        consultation.UsesCareBenefit = usingCare;
        consultation.Status = ConsultationStatus.Scheduled;
        consultation.ResponsibleName = details.Provider?.BusinessName;
        await _db.TouchAsync(consultation, ct);

        _db.Notifications.Add(new AppNotification
        {
            UserId = command.ClientId,
            Title = CatalogLocalizer.Loc("Consulta reservada", "Consultation booked"),
            Message = CatalogLocalizer.Loc(
                $"Tu consulta ({item.Code}) quedó pendiente de confirmación.",
                $"Your consultation ({item.Code}) is pending confirmation."),
            Type = "vet-consultation",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("payment_completed", command.ClientId, "Consultation", consultation.Id,
            new { price = charge, care = usingCare, code = item.Code, appointmentId = appointment.Id }, ct);

        return new BookConsultationResult(BookConsultationOutcome.Booked, details, usingCare);
    }

    /// <summary>The provider's first service, created from the catalog item when it has none.</summary>
    private async Task<GroomerService> ProviderServiceAsync(int providerId, ServiceCatalogItem item, CancellationToken ct)
    {
        var service = await _db.Services.FirstOrDefaultAsync(s => s.GroomerId == providerId, ct);
        if (service is not null)
            return service;

        service = new GroomerService
        {
            GroomerId = providerId,
            Name = item.NameEs,
            Description = item.ScopeEs,
            DurationMinutes = item.DurationMinutes,
            PriceSmall = item.Price,
            PriceMedium = item.Price,
            PriceLarge = item.Price,
            PriceGiant = item.Price
        };
        _db.Services.Add(service);
        await _db.SaveChangesAsync(ct);
        return service;
    }
}
