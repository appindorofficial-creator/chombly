using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Consultations.BookConsultation;
using WebAppPet.Application.Consultations.EscalateToEmergency;
using WebAppPet.Application.Consultations.GetConsultationCheckout;
using WebAppPet.Application.Consultations.GetConsultationSummary;
using WebAppPet.Application.Consultations.SelectLocalVet;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Consultations;

public class ConsultationBookingTests : ConsultationTestBase
{
    private GetConsultationCheckoutHandler Checkout => new(Db, HomeCountry, Catalog, Care, Audit);
    private BookConsultationHandler Book => new(Db, Checkout, Care, new ConsentService(Db), Audit, TestData.Payments(Db));

    private GroomerProfile AddVet(decimal startingPrice = 0, string? licensedIn = null)
    {
        var vet = TestData.AddBusiness(Db);
        vet.IsActive = true;
        vet.PublishStatus = BusinessPublishStatus.Approved;
        vet.VetProviderKind = VetProviderKind.LocalVet;
        vet.StartingPrice = startingPrice;
        if (licensedIn is not null)
            Db.ProviderLicenses.Add(new ProviderLicense { GroomerId = vet.Id, Jurisdiction = licensedIn, LicenseNumber = "L-1", IsVerified = true });
        Db.SaveChanges();
        return vet;
    }

    private Consultation ReadyForCheckout(AppUser client, GroomerProfile vet, string code = ServiceCatalogCodes.VetIntl30)
    {
        var consultation = AddConsultation(client, AddPet(client), ConsultationStatus.ProviderSelected, code);
        consultation.ProviderId = vet.Id;
        consultation.ScheduledAt = new DateTime(2026, 10, 1, 15, 0, 0, DateTimeKind.Utc);
        consultation.Symptoms = "Tos";
        Db.SaveChanges();
        return consultation;
    }

    private PaymentMethod AddCard(AppUser client, bool isDefault = true, string last4 = "4242")
    {
        var card = new PaymentMethod { UserId = client.Id, Brand = "Visa", Last4 = last4, HolderName = "Ana", ExpMonth = 12, ExpYear = 2035, IsDefault = isDefault };
        Db.PaymentMethods.Add(card);
        Db.SaveChanges();
        return card;
    }

    private static BookConsultationCommand Command(Consultation c, bool care = false, int? cardId = null, bool terms = true) =>
        new(c.ClientId, c.Id, terms, AcceptScope: true, AcceptMedia: false, cardId, care, "10.0.0.1", "Tests");

    [Fact]
    public async Task Paying_by_card_books_a_pending_appointment_at_the_providers_price()
    {
        var client = AddClient();
        AddCard(client);
        var vet = AddVet(startingPrice: 25);
        var consultation = ReadyForCheckout(client, vet);

        var result = await Book.HandleAsync(Command(consultation));

        Assert.Equal(BookConsultationOutcome.Booked, result.Outcome);
        var appointment = await Db.Appointments.AsNoTracking().SingleAsync();
        Assert.Equal((AppointmentStatus.Pending, 25m, 25m, vet.Id), (appointment.Status, appointment.TotalPrice, appointment.DepositPaid, appointment.GroomerId));
        Assert.Equal($"Vet consultation #{consultation.Id} (vet_intl_30) | Card Visa •••• 4242. Symptoms: Tos", appointment.Notes);
        Assert.Equal("Orientación 30 min", (await Db.Services.AsNoTracking().SingleAsync(s => s.Id == appointment.ServiceId)).Name);

        var saved = Reload(consultation.Id);
        Assert.Equal((ConsultationStatus.Scheduled, appointment.Id, 25m, false, vet.BusinessName),
            (saved.Status, saved.AppointmentId, saved.PriceCharged, saved.UsesCareBenefit, saved.ResponsibleName));
        Assert.Equal(
            [ConsentService.DocTerms, ConsentService.DocPrivacy, ConsentService.DocIntlOrientation],
            await Db.ConsentRecords.OrderBy(r => r.Id).Select(r => r.DocumentKey).ToListAsync());
        Assert.Equal("vet-consultation", (await Db.Notifications.SingleAsync()).Type);
        Assert.Equal(["payment_succeeded", "payment_completed"], AuditActions());

        var charge = await Db.PaymentTransactions.AsNoTracking().SingleAsync();
        Assert.Equal(
            (PaymentTransactionStatus.Succeeded, PaymentPurpose.VetConsultation, 25m, 25m, vet.Id, consultation.Id, appointment.Id),
            (charge.Status, charge.Purpose, charge.Amount, charge.ServiceTotal, charge.ProviderId, charge.ConsultationId, charge.AppointmentId));
    }

    [Fact]
    public async Task A_declined_card_books_nothing_and_records_the_attempt()
    {
        var client = AddClient();
        AddCard(client, last4: "9995");
        var consultation = ReadyForCheckout(client, AddVet(startingPrice: 25));

        var result = await Book.HandleAsync(Command(consultation));

        Assert.Equal(BookConsultationOutcome.PaymentDeclined, result.Outcome);
        Assert.False(string.IsNullOrEmpty(result.PaymentError));
        Assert.False(await Db.Appointments.AnyAsync());
        Assert.Equal(ConsultationStatus.ProviderSelected, Reload(consultation.Id).Status);
        var attempt = await Db.PaymentTransactions.AsNoTracking().SingleAsync();
        Assert.Equal((PaymentTransactionStatus.Failed, "insufficient_funds"), (attempt.Status, attempt.FailureCode));
    }

    [Fact]
    public async Task Paying_with_Care_charges_nothing_and_uses_the_quick_consult()
    {
        var client = AddClient();
        AddCare(client);
        var consultation = ReadyForCheckout(client, AddVet(startingPrice: 25));

        var result = await Book.HandleAsync(Command(consultation, care: true));

        Assert.Equal(BookConsultationOutcome.Booked, result.Outcome);
        Assert.True(result.UsingCare);
        Assert.Equal(0m, (await Db.Appointments.AsNoTracking().SingleAsync()).TotalPrice);
        Assert.Equal(consultation.Id, (await Db.CareBenefitUses.SingleAsync()).ConsultationId);
        Assert.True(Reload(consultation.Id).UsesCareBenefit);
        Assert.False(await Db.PaymentTransactions.AnyAsync());
    }

    [Fact]
    public async Task Without_Care_left_or_a_card_nothing_is_booked()
    {
        var client = AddClient();
        var consultation = ReadyForCheckout(client, AddVet());

        var result = await Book.HandleAsync(Command(consultation, care: true));

        Assert.Equal(BookConsultationOutcome.NoPaymentMethod, result.Outcome);
        Assert.False(await Db.Appointments.AnyAsync());
        Assert.Equal(30m, result.Details!.CatalogPrice);
    }

    [Fact]
    public async Task Another_clients_card_cannot_be_charged()
    {
        var client = AddClient();
        AddCard(client);
        var strangersCard = AddCard(AddClient());
        var consultation = ReadyForCheckout(client, AddVet());

        var result = await Book.HandleAsync(Command(consultation, cardId: strangersCard.Id));

        Assert.Equal(BookConsultationOutcome.NoPaymentMethod, result.Outcome);
    }

    [Fact]
    public async Task Terms_must_be_accepted_and_a_provider_chosen()
    {
        var client = AddClient();
        AddCard(client);
        var ready = ReadyForCheckout(client, AddVet());
        var noProviderYet = AddConsultation(client, AddPet(client), ConsultationStatus.SafetyScreened);

        Assert.Equal(BookConsultationOutcome.Incomplete, (await Book.HandleAsync(Command(ready, terms: false))).Outcome);
        Assert.Equal(BookConsultationOutcome.NotFound, (await Book.HandleAsync(Command(noProviderYet))).Outcome);
        Assert.False(await Db.Appointments.AnyAsync());
    }

    [Fact]
    public async Task Opening_checkout_logs_the_start_and_prefers_the_default_card()
    {
        var client = AddClient();
        AddCard(client, isDefault: false);
        var preferred = AddCard(client, isDefault: true);
        var consultation = ReadyForCheckout(client, AddVet());

        var details = await Checkout.HandleAsync(new GetConsultationCheckoutQuery(client.Id, consultation.Id, LogStarted: true));

        Assert.NotNull(details);
        Assert.Equal(preferred.Id, details.DefaultPayment!.Id);
        Assert.Equal(preferred.Id, details.Payments[0].Id);
        Assert.Equal(["checkout_started"], AuditActions());
    }

    [Fact]
    public async Task Selecting_a_licensed_local_vet_schedules_the_consultation()
    {
        var client = AddClient("US");
        var pet = AddPet(client);
        var vet = AddVet(licensedIn: "NC");
        var consultation = AddConsultation(client, pet, ConsultationStatus.EligibilityVerified, ServiceCatalogCodes.VetLocal30, "US", "NC");
        consultation.HasActiveVcpr = true;
        Db.SaveChanges();
        var handler = new SelectLocalVetHandler(Db, HomeCountry, Catalog);

        var invalid = await handler.HandleAsync(new SelectLocalVetCommand(client.Id, consultation.Id, vet.Id, "25:99", "hoy"));
        var selected = await handler.HandleAsync(new SelectLocalVetCommand(client.Id, consultation.Id, vet.Id, "10:30 AM", "tomorrow"));

        Assert.Null(invalid.NextStep);
        Assert.Equal(vet.Id, Assert.Single(invalid.Options!.Providers).Id);
        Assert.Equal(ConsultationStep.Checkout, selected.NextStep);
        var saved = Reload(consultation.Id);
        Assert.Equal((vet.Id, ConsultationStatus.ProviderSelected), (saved.ProviderId, saved.Status));
        Assert.NotNull(saved.ScheduledAt);
        Assert.True(saved.ScheduledAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task A_local_vet_needs_a_US_client_with_an_active_VCPR()
    {
        var colombian = AddClient("CO");
        var american = AddClient("US");
        var vet = AddVet(licensedIn: "NC");
        var handler = new SelectLocalVetHandler(Db, HomeCountry, Catalog);

        var fromColombia = await handler.HandleAsync(new SelectLocalVetCommand(colombian.Id, AddConsultation(colombian).Id, vet.Id, "10:30 AM", null));
        var withoutVcpr = await handler.HandleAsync(new SelectLocalVetCommand(american.Id, AddConsultation(american, country: "US").Id, vet.Id, "10:30 AM", null));

        Assert.Equal(ConsultationStep.IntlMatches, fromColombia.NextStep);
        Assert.Equal(ConsultationStep.Eligibility, withoutVcpr.NextStep);
    }

    [Fact]
    public async Task Emergency_escalates_the_clients_own_consultation_only()
    {
        var client = AddClient();
        var mine = AddConsultation(client);
        var someoneElses = AddConsultation(AddClient());
        var handler = new EscalateToEmergencyHandler(Db, Care, Audit);

        await handler.HandleAsync(new EscalateToEmergencyCommand(client.Id, mine.Id, "CO"));
        await handler.HandleAsync(new EscalateToEmergencyCommand(client.Id, someoneElses.Id, "CO"));

        Assert.Equal((ConsultationStatus.EscalatedToEmergency, VetModality.Emergency), (Reload(mine.Id).Status, Reload(mine.Id).Modality));
        Assert.Equal(ConsultationStatus.Draft, Reload(someoneElses.Id).Status);
        Assert.Equal(["escalated_emergency", "escalated_emergency"], AuditActions());
    }

    [Fact]
    public async Task Visitors_who_are_not_signed_in_see_clinics_without_escalating_any_consultation()
    {
        var consultation = AddConsultation(AddClient());

        await new EscalateToEmergencyHandler(Db, Care, Audit).HandleAsync(new EscalateToEmergencyCommand(null, consultation.Id, "CO"));

        Assert.Equal(ConsultationStatus.Draft, Reload(consultation.Id).Status);
        Assert.Null((await Db.AuditLogs.SingleAsync()).EntityId);
    }

    [Fact]
    public async Task When_the_Care_benefit_cannot_be_used_nothing_is_booked()
    {
        var client = AddClient();
        AddCare(client);
        var consultation = ReadyForCheckout(client, AddVet(startingPrice: 25));
        var book = new BookConsultationHandler(Db, Checkout, new ExhaustedCare(Db), new ConsentService(Db), Audit, TestData.Payments(Db));

        var result = await book.HandleAsync(Command(consultation, care: true));

        Assert.Equal(BookConsultationOutcome.CareBenefitFailed, result.Outcome);
        Assert.False(await Db.Appointments.AnyAsync());
        Assert.False(await Db.ConsentRecords.AnyAsync());
        Assert.Equal(ConsultationStatus.ProviderSelected, Reload(consultation.Id).Status);
    }

    [Fact]
    public async Task The_summary_of_an_unbooked_consultation_resumes_the_flow()
    {
        var client = AddClient();
        AddCard(client);
        var draft = AddConsultation(client);
        var awaitingPayment = ReadyForCheckout(client, AddVet());
        var booked = ReadyForCheckout(client, AddVet());
        await Book.HandleAsync(Command(booked));
        var handler = new GetConsultationSummaryHandler(Db, HomeCountry, Catalog);

        Assert.Equal(ConsultationStep.Pet, (await handler.HandleAsync(new GetConsultationSummaryQuery(client.Id, draft.Id)))!.ResumeStep);
        Assert.Equal(ConsultationStep.Checkout, (await handler.HandleAsync(new GetConsultationSummaryQuery(client.Id, awaitingPayment.Id)))!.ResumeStep);
        Assert.Null((await handler.HandleAsync(new GetConsultationSummaryQuery(client.Id, booked.Id)))!.ResumeStep);
        Assert.Null(await handler.HandleAsync(new GetConsultationSummaryQuery(AddClient().Id, booked.Id)));
    }

    private sealed class ExhaustedCare(AppDbContext db) : ChomblyCareService(db)
    {
        public override Task<bool> TryConsumeQuickConsultAsync(int userId, int consultationId, CancellationToken ct = default) =>
            Task.FromResult(false);
    }
}
