using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Behavior.GetBehaviorProviders;
using WebAppPet.Application.Behavior.Shared;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Localization;
using WebAppPet.Pages.Shared;
using WebAppPet.Services;

namespace WebAppPet.Application.Behavior.BookBehaviorSession;

/// <summary>
/// Books one pending session per selected dog with the chosen specialist, each charged in full
/// to the card. When one charge is declined the ones already made are refunded and nothing is booked.
/// </summary>
public class BookBehaviorSessionHandler
{
    private readonly AppDbContext _db;
    private readonly GetBehaviorProvidersHandler _providers;
    private readonly PaymentService _payments;
    private readonly ConsentService _consent;
    private readonly VetAuditService _audit;

    public BookBehaviorSessionHandler(
        AppDbContext db,
        GetBehaviorProvidersHandler providers,
        PaymentService payments,
        ConsentService consent,
        VetAuditService audit)
    {
        _db = db;
        _providers = providers;
        _payments = payments;
        _consent = consent;
        _audit = audit;
    }

    public async Task<BookBehaviorSessionResult> HandleAsync(BookBehaviorSessionCommand command, CancellationToken ct = default)
    {
        var options = await _providers.HandleAsync(new GetBehaviorProvidersQuery(
            command.ClientId, command.CaseId, command.ProviderId, command.Date, command.When, command.Slot), ct);
        BookBehaviorSessionResult Stop(BookBehaviorSessionOutcome outcome, string? paymentError = null) => new(outcome, options, paymentError);

        if (options.Redirect is not null)
            return Stop(BookBehaviorSessionOutcome.Redirect);
        if (!command.AcceptTerms)
            return Stop(BookBehaviorSessionOutcome.TermsNotAccepted);
        if (!BookingDate.TryParseSelected(options.Date, out var day))
            return Stop(BookBehaviorSessionOutcome.NoDate);
        if (options.Slot is null || !AppTimeZones.TryParseSlotToTimeSpan(options.Slot, out var timeOfDay))
            return Stop(BookBehaviorSessionOutcome.NoSlot);
        if (options.ProviderId <= 0)
            return Stop(BookBehaviorSessionOutcome.NoProvider);

        var scheduledAt = AppTimeZones.LocalDateAndTimeToUtc(day, timeOfDay);
        if (scheduledAt <= DateTime.UtcNow)
            return Stop(BookBehaviorSessionOutcome.PastTime);
        if (await _db.Appointments.AsNoTracking().AnyAsync(a =>
                a.GroomerId == options.ProviderId
                && a.Status != AppointmentStatus.Cancelled
                && a.ScheduledAt == scheduledAt, ct))
            return Stop(BookBehaviorSessionOutcome.SlotTaken);

        var dogs = options.SelectedDogs;
        if (dogs.Count == 0 || options.CatalogItem is not ServiceCatalogItem item)
            return Stop(BookBehaviorSessionOutcome.Incomplete);

        var behaviorCase = options.Case!;
        var charges = new List<PaymentTransaction>();
        if (item.Price > 0)
        {
            var card = await _payments.FindCardAsync(command.ClientId, command.PaymentMethodId, ct);
            if (card is null)
                return Stop(BookBehaviorSessionOutcome.NoPaymentMethod);

            foreach (var dog in dogs)
            {
                var charge = await _payments.ChargeAsync(new ChargeRequest
                {
                    UserId = command.ClientId,
                    Card = card,
                    Amount = item.Price,
                    Purpose = PaymentPurpose.BehaviorSession,
                    Description = $"Behavior case #{behaviorCase.Id} · {dog.Name}",
                    ProviderId = options.ProviderId,
                    BehaviorCaseId = behaviorCase.Id
                }, ct);
                if (charge.Status == PaymentTransactionStatus.Succeeded)
                {
                    charges.Add(charge);
                    continue;
                }

                foreach (var done in charges)
                    await _payments.RefundAsync(done, "Another session of the same booking was declined", command.ClientId, ct);
                return Stop(BookBehaviorSessionOutcome.PaymentDeclined, PaymentFailureCodes.Message(charge.FailureCode));
            }
        }

        behaviorCase.ProviderId = options.ProviderId;
        behaviorCase.ScheduledAt = scheduledAt;
        var service = await ProviderServiceAsync(options.ProviderId, item, ct);

        var appointments = dogs.Select(dog => new Appointment
        {
            ClientId = command.ClientId,
            PetId = dog.Id,
            GroomerId = options.ProviderId,
            ServiceId = service.Id,
            ScheduledAt = scheduledAt,
            Status = AppointmentStatus.Pending,
            TotalPrice = item.Price,
            DepositPaid = item.Price,
            Notes = AppointmentNotes(behaviorCase, dogs),
            CreatedAt = DateTime.UtcNow
        }).ToList();
        _db.Appointments.AddRange(appointments);
        await _db.SaveChangesAsync(ct);
        for (var i = 0; i < charges.Count; i++)
            await _payments.AttachAppointmentAsync(charges[i], appointments[i].Id, ct);

        await _consent.SaveAsync(command.ClientId, null, new[]
        {
            (ConsentService.DocTerms, true),
            ("behavior_scope", true)
        }, command.IpAddress, command.UserAgent, ct);

        behaviorCase.AppointmentId = appointments[0].Id;
        behaviorCase.PriceCharged = item.Price * dogs.Count;
        behaviorCase.Status = BehaviorCaseStatus.Scheduled;
        behaviorCase.Goals = CatalogLocalizer.Loc(
            $"Reducir: {behaviorCase.ProblemType}",
            $"Reduce: {behaviorCase.ProblemType}");
        behaviorCase.Exercises = CatalogLocalizer.Loc(
            "El especialista definirá los ejercicios en la sesión.",
            "The specialist will define exercises in the session.");
        behaviorCase.FollowUpNotes = behaviorCase.ContextNotes;
        await _db.TouchAsync(behaviorCase, ct);

        await _audit.LogAsync("behavior_booked", command.ClientId, "BehaviorCase", behaviorCase.Id,
            new
            {
                price = behaviorCase.PriceCharged,
                providerId = behaviorCase.ProviderId,
                appointmentId = behaviorCase.AppointmentId,
                petIds = dogs.Select(p => p.Id).ToArray(),
                scheduledAt
            }, ct);

        return Stop(BookBehaviorSessionOutcome.Booked);
    }

    private static string AppointmentNotes(BehaviorCase behaviorCase, List<Pet> dogs)
    {
        var notes = $"Behavior case #{behaviorCase.Id}: {behaviorCase.ProblemType}";
        if (!string.IsNullOrWhiteSpace(behaviorCase.Frequency))
            notes += $" · {behaviorCase.Frequency}";
        if (!string.IsNullOrWhiteSpace(behaviorCase.ContextNotes))
            notes += $". {behaviorCase.ContextNotes}";
        if (dogs.Count > 1)
            notes += $" · pets: {string.Join(", ", dogs.Select(p => p.Name))}";
        return notes;
    }

    /// <summary>The specialist's first service, created from the catalog item when it has none.</summary>
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
