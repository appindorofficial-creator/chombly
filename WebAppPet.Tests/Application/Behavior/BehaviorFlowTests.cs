using WebAppPet.Application.Behavior.BookBehaviorSession;
using WebAppPet.Application.Behavior.GetBehaviorIntake;
using WebAppPet.Application.Behavior.GetBehaviorProviders;
using WebAppPet.Application.Behavior.SaveBehaviorFollowUp;
using WebAppPet.Application.Behavior.Shared;
using WebAppPet.Application.Behavior.StartBehaviorCase;
using WebAppPet.Application.Behavior.SubmitBehaviorIntake;
using WebAppPet.Application.Common;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Behavior;

public class BehaviorFlowTests : IDisposable
{
    private const decimal SessionPrice = 50;
    private const string Slot = "10:00 AM";

    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly string _tomorrow = AppTimeZones.TodayLocalDate().AddDays(1).ToString("yyyy-MM-dd");

    public BehaviorFlowTests()
    {
        _db = _database.CreateContext();
        _db.ServiceCatalog.Add(new ServiceCatalogItem
        {
            Code = ServiceCatalogCodes.BehaviorSession,
            NameEs = "Sesión de conducta",
            ScopeEs = "Conducta",
            Price = SessionPrice,
            DurationMinutes = 60,
            IsBookable = false
        });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private GetBehaviorIntakeHandler Intake => new(_db);
    private SubmitBehaviorIntakeHandler Submit => new(_db, Intake, new VetAuditService(_db));
    private GetBehaviorProvidersHandler Providers => new(_db, new ServiceCatalogService(_db));
    private BookBehaviorSessionHandler Book => new(_db, Providers, TestData.Payments(_db), new ConsentService(_db), new VetAuditService(_db));

    private GroomerProfile AddSpecialist(BusinessPublishStatus status = BusinessPublishStatus.Approved)
    {
        var specialist = TestData.AddBusiness(_db);
        specialist.VetProviderKind = VetProviderKind.BehaviorSpecialist;
        specialist.PublishStatus = status;
        specialist.IsActive = true;
        specialist.LicenseCountry = "CO";
        specialist.City = "Neiva";
        _db.SaveChanges();
        return specialist;
    }

    private BehaviorCase AddCase(AppUser client, BehaviorCaseStatus status, params Pet[] dogs)
    {
        var behaviorCase = new BehaviorCase { ClientId = client.Id, Status = status, ProblemType = "Ladridos excesivos", Frequency = "Diario" };
        BehaviorCasePets.SetSelectedIds(behaviorCase, dogs.Select(d => d.Id));
        _db.BehaviorCases.Add(behaviorCase);
        _db.SaveChanges();
        return behaviorCase;
    }

    private BookBehaviorSessionCommand BookCommand(AppUser client, BehaviorCase behaviorCase, GroomerProfile specialist,
        bool acceptTerms = true, int? cardId = null, string slot = Slot) =>
        new(client.Id, behaviorCase.Id, specialist.Id, _tomorrow, null, slot, acceptTerms, cardId, "127.0.0.1", "tests");

    private BehaviorCase Reload(int id)
    {
        using var db = _database.CreateContext();
        return db.BehaviorCases.Single(c => c.Id == id);
    }

    [Fact]
    public async Task Start_opens_a_draft_and_ignores_a_pet_that_is_not_the_clients()
    {
        var client = TestData.AddUser(_db);
        var otherPet = TestData.AddPet(_db, TestData.AddUser(_db));

        var caseId = await new StartBehaviorCaseHandler(_db).HandleAsync(new StartBehaviorCaseCommand(client.Id, otherPet.Id));

        var saved = Reload(caseId);
        Assert.Equal(client.Id, saved.ClientId);
        Assert.Equal(BehaviorCaseStatus.Draft, saved.Status);
        Assert.Null(saved.PetId);
    }

    [Fact]
    public async Task Intake_only_offers_the_clients_dogs()
    {
        var client = TestData.AddUser(_db);
        var dog = TestData.AddPet(_db, client);
        TestData.AddPet(_db, client, PetSpecies.Cat);
        TestData.AddPet(_db, TestData.AddUser(_db));
        var behaviorCase = AddCase(client, BehaviorCaseStatus.Draft);

        var form = await Intake.HandleAsync(new GetBehaviorIntakeQuery(client.Id, behaviorCase.Id));

        Assert.Equal([dog.Id], form!.Dogs.Select(p => p.Id));
        Assert.Null(await Intake.HandleAsync(new GetBehaviorIntakeQuery(TestData.AddUser(_db).Id, behaviorCase.Id)));
    }

    [Fact]
    public async Task Submitting_the_intake_keeps_only_the_clients_dogs_and_completes_it()
    {
        var client = TestData.AddUser(_db);
        var first = TestData.AddPet(_db, client);
        var second = TestData.AddPet(_db, client);
        var cat = TestData.AddPet(_db, client, PetSpecies.Cat);
        var behaviorCase = AddCase(client, BehaviorCaseStatus.Draft);

        var result = await Submit.HandleAsync(new SubmitBehaviorIntakeCommand(
            client.Id, behaviorCase.Id, [second.Id, cat.Id, first.Id, second.Id], " Tirones en paseo ", "Diario", " Solo en la calle ", false));

        Assert.Equal(SubmitBehaviorIntakeOutcome.Completed, result.Outcome);
        var saved = Reload(behaviorCase.Id);
        Assert.Equal(BehaviorCaseStatus.IntakeComplete, saved.Status);
        Assert.Equal([second.Id, first.Id], BehaviorCasePets.SelectedIds(saved));
        Assert.Equal("Tirones en paseo", saved.ProblemType);
        Assert.Equal("Solo en la calle", saved.ContextNotes);
    }

    [Theory]
    [InlineData(false, "Ladridos", "Diario", SubmitBehaviorIntakeOutcome.NoDogSelected)]
    [InlineData(true, " ", "Diario", SubmitBehaviorIntakeOutcome.NoProblem)]
    [InlineData(true, "Ladridos", null, SubmitBehaviorIntakeOutcome.NoFrequency)]
    public async Task An_incomplete_intake_is_not_saved(bool withDog, string? problem, string? frequency, SubmitBehaviorIntakeOutcome expected)
    {
        var client = TestData.AddUser(_db);
        var dog = TestData.AddPet(_db, client);
        var behaviorCase = AddCase(client, BehaviorCaseStatus.Draft);

        var result = await Submit.HandleAsync(new SubmitBehaviorIntakeCommand(
            client.Id, behaviorCase.Id, withDog ? [dog.Id] : [], problem, frequency, null, false));

        Assert.Equal(expected, result.Outcome);
        Assert.NotNull(result.Form);
        Assert.Equal(BehaviorCaseStatus.Draft, Reload(behaviorCase.Id).Status);
    }

    [Fact]
    public async Task A_clinical_concern_refers_the_case_to_a_vet()
    {
        var client = TestData.AddUser(_db);
        var dog = TestData.AddPet(_db, client);
        var behaviorCase = AddCase(client, BehaviorCaseStatus.Draft);

        var result = await Submit.HandleAsync(new SubmitBehaviorIntakeCommand(
            client.Id, behaviorCase.Id, [dog.Id], "Agresión a personas", "Diario", null, true));

        Assert.Equal(SubmitBehaviorIntakeOutcome.ReferredToVet, result.Outcome);
        var saved = Reload(behaviorCase.Id);
        Assert.Equal(BehaviorCaseStatus.ReferredToVet, saved.Status);
        Assert.True(saved.ClinicalRedFlag);
        Assert.Contains(_db.AuditLogs, a => a.Action == "behavior_referred_vet" && a.EntityId == behaviorCase.Id);
    }

    [Theory]
    [InlineData(BehaviorCaseStatus.Draft, BehaviorStep.Intake)]
    [InlineData(BehaviorCaseStatus.ReferredToVet, BehaviorStep.VetHome)]
    [InlineData(BehaviorCaseStatus.Scheduled, BehaviorStep.Summary)]
    [InlineData(BehaviorCaseStatus.PlanActive, BehaviorStep.Summary)]
    public async Task Picking_a_specialist_redirects_cases_that_are_not_ready(BehaviorCaseStatus status, BehaviorStep expected)
    {
        var client = TestData.AddUser(_db);
        var behaviorCase = AddCase(client, status, TestData.AddPet(_db, client));

        var options = await Providers.HandleAsync(new GetBehaviorProvidersQuery(client.Id, behaviorCase.Id, 0, null, null, null));

        Assert.Equal(expected, options.Redirect);
    }

    [Fact]
    public async Task Another_clients_case_goes_back_to_care_services()
    {
        var behaviorCase = AddCase(TestData.AddUser(_db), BehaviorCaseStatus.IntakeComplete);

        var options = await Providers.HandleAsync(new GetBehaviorProvidersQuery(TestData.AddUser(_db).Id, behaviorCase.Id, 0, null, null, null));

        Assert.Equal(BehaviorStep.CareServices, options.Redirect);
    }

    [Fact]
    public async Task Only_approved_behavior_specialists_are_offered_and_taken_slots_are_marked()
    {
        var client = TestData.AddUser(_db);
        var behaviorCase = AddCase(client, BehaviorCaseStatus.IntakeComplete, TestData.AddPet(_db, client));
        var specialist = AddSpecialist();
        AddSpecialist(BusinessPublishStatus.PendingReview);
        TestData.AddBusiness(_db);

        var day = AppTimeZones.TodayLocalDate().AddDays(1);
        AppTimeZones.TryParseSlotToTimeSpan(Slot, out var timeOfDay);
        TestData.AddAppointment(_db, TestData.AddUser(_db), specialist, AppointmentStatus.Confirmed,
            AppTimeZones.LocalDateAndTimeToUtc(day, timeOfDay));

        var options = await Providers.HandleAsync(new GetBehaviorProvidersQuery(client.Id, behaviorCase.Id, specialist.Id, _tomorrow, null, Slot));

        Assert.Null(options.Redirect);
        Assert.Equal([specialist.Id], options.Providers.Select(p => p.Id));
        Assert.Equal(specialist.Id, options.ProviderId);
        Assert.Contains(Slot, options.OccupiedSlots);
        Assert.Null(options.Slot);
    }

    [Fact]
    public async Task Legacy_when_is_normalized_and_unknown_specialists_are_dropped()
    {
        var client = TestData.AddUser(_db);
        var behaviorCase = AddCase(client, BehaviorCaseStatus.IntakeComplete, TestData.AddPet(_db, client));
        AddSpecialist();

        var options = await Providers.HandleAsync(new GetBehaviorProvidersQuery(client.Id, behaviorCase.Id, 9999, null, "manana", Slot));

        Assert.Equal(_tomorrow, options.Date);
        Assert.Equal(0, options.ProviderId);
        Assert.Equal(Slot, options.Slot);
    }

    [Fact]
    public async Task Booking_charges_every_dog_the_colombian_price_and_creates_one_pending_session_each()
    {
        var client = TestData.AddUser(_db);
        var first = TestData.AddPet(_db, client);
        var second = TestData.AddPet(_db, client);
        var card = TestData.AddCard(_db, client);
        var behaviorCase = AddCase(client, BehaviorCaseStatus.IntakeComplete, first, second);
        var specialist = AddSpecialist();

        var result = await Book.HandleAsync(BookCommand(client, behaviorCase, specialist, cardId: card.Id));

        Assert.Equal(BookBehaviorSessionOutcome.Booked, result.Outcome);
        var appointments = _db.Appointments.Where(a => a.GroomerId == specialist.Id).OrderBy(a => a.Id).ToList();
        Assert.Equal([first.Id, second.Id], appointments.Select(a => a.PetId));
        Assert.All(appointments, a =>
        {
            Assert.Equal(AppointmentStatus.Pending, a.Status);
            Assert.Equal(MarketPrices.ColombiaBehaviorSession, a.TotalPrice);
            Assert.Equal(MarketPrices.ColombiaBehaviorSession, a.DepositPaid);
        });

        var charges = _db.PaymentTransactions.Where(t => t.BehaviorCaseId == behaviorCase.Id).OrderBy(t => t.Id).ToList();
        Assert.Equal(2, charges.Count);
        Assert.All(charges, t =>
        {
            Assert.Equal(PaymentTransactionStatus.Succeeded, t.Status);
            Assert.Equal(PaymentPurpose.BehaviorSession, t.Purpose);
            Assert.Equal(MarketPrices.ColombiaBehaviorSession, t.Amount);
            Assert.Equal(specialist.Id, t.ProviderId);
        });
        Assert.Equal(appointments.Select(a => (int?)a.Id), charges.Select(t => t.AppointmentId));

        var saved = Reload(behaviorCase.Id);
        Assert.Equal(BehaviorCaseStatus.Scheduled, saved.Status);
        Assert.Equal(specialist.Id, saved.ProviderId);
        Assert.Equal(appointments[0].Id, saved.AppointmentId);
        Assert.Equal(MarketPrices.ColombiaBehaviorSession * 2, saved.PriceCharged);
        Assert.Contains(_db.AuditLogs, a => a.Action == "behavior_booked" && a.EntityId == behaviorCase.Id);
    }

    [Fact]
    public async Task Each_specialist_is_offered_and_charged_at_their_own_price()
    {
        var client = TestData.AddUser(_db);
        var card = TestData.AddCard(_db, client);
        var behaviorCase = AddCase(client, BehaviorCaseStatus.IntakeComplete, TestData.AddPet(_db, client));
        var ownPrice = AddSpecialist();
        ownPrice.StartingPrice = 80_000;
        var marketPrice = AddSpecialist();
        _db.SaveChanges();

        var options = await Providers.HandleAsync(new GetBehaviorProvidersQuery(client.Id, behaviorCase.Id, 0, null, null, null));

        Assert.Equal(80_000, options.PriceOf(options.Providers.Single(p => p.Id == ownPrice.Id)));
        Assert.Equal(MarketPrices.ColombiaBehaviorSession, options.PriceOf(options.Providers.Single(p => p.Id == marketPrice.Id)));
        Assert.Equal(MarketPrices.ColombiaBehaviorSession, options.UnitPrice);
        Assert.True(options.PriceVaries);

        var result = await Book.HandleAsync(BookCommand(client, behaviorCase, ownPrice, cardId: card.Id));

        Assert.Equal(BookBehaviorSessionOutcome.Booked, result.Outcome);
        Assert.Equal(80_000, _db.PaymentTransactions.Single(t => t.BehaviorCaseId == behaviorCase.Id).Amount);
        Assert.Equal(80_000, _db.Appointments.Single(a => a.GroomerId == ownPrice.Id).TotalPrice);
        Assert.Equal(80_000, Reload(behaviorCase.Id).PriceCharged);
    }

    [Fact]
    public async Task Before_choosing_the_price_shown_is_the_only_specialists_own_price()
    {
        var client = TestData.AddUser(_db);
        var behaviorCase = AddCase(client, BehaviorCaseStatus.IntakeComplete, TestData.AddPet(_db, client));
        AddSpecialist().StartingPrice = 75_000;
        _db.SaveChanges();

        var options = await Providers.HandleAsync(new GetBehaviorProvidersQuery(client.Id, behaviorCase.Id, 0, null, null, null));

        Assert.Equal(75_000, options.UnitPrice);
        Assert.False(options.PriceVaries);
    }

    [Fact]
    public async Task A_declined_card_books_nothing()
    {
        var client = TestData.AddUser(_db);
        TestData.AddCard(_db, client, last4: "0002");
        var behaviorCase = AddCase(client, BehaviorCaseStatus.IntakeComplete, TestData.AddPet(_db, client));
        var specialist = AddSpecialist();

        var result = await Book.HandleAsync(BookCommand(client, behaviorCase, specialist));

        Assert.Equal(BookBehaviorSessionOutcome.PaymentDeclined, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.PaymentError));
        Assert.Empty(_db.Appointments);
        Assert.DoesNotContain(_db.PaymentTransactions, t => t.Status == PaymentTransactionStatus.Succeeded);
        Assert.Equal(BehaviorCaseStatus.IntakeComplete, Reload(behaviorCase.Id).Status);
    }

    [Fact]
    public async Task Booking_without_a_card_asks_for_one()
    {
        var client = TestData.AddUser(_db);
        var behaviorCase = AddCase(client, BehaviorCaseStatus.IntakeComplete, TestData.AddPet(_db, client));

        var result = await Book.HandleAsync(BookCommand(client, behaviorCase, AddSpecialist()));

        Assert.Equal(BookBehaviorSessionOutcome.NoPaymentMethod, result.Outcome);
        Assert.Empty(_db.Appointments);
    }

    [Fact]
    public async Task Booking_requires_accepting_the_terms()
    {
        var client = TestData.AddUser(_db);
        TestData.AddCard(_db, client);
        var behaviorCase = AddCase(client, BehaviorCaseStatus.IntakeComplete, TestData.AddPet(_db, client));

        var result = await Book.HandleAsync(BookCommand(client, behaviorCase, AddSpecialist(), acceptTerms: false));

        Assert.Equal(BookBehaviorSessionOutcome.TermsNotAccepted, result.Outcome);
        Assert.Empty(_db.PaymentTransactions);
    }

    [Fact]
    public async Task A_taken_slot_cannot_be_booked()
    {
        var client = TestData.AddUser(_db);
        TestData.AddCard(_db, client);
        var behaviorCase = AddCase(client, BehaviorCaseStatus.IntakeComplete, TestData.AddPet(_db, client));
        var specialist = AddSpecialist();
        AppTimeZones.TryParseSlotToTimeSpan(Slot, out var timeOfDay);
        TestData.AddAppointment(_db, TestData.AddUser(_db), specialist, AppointmentStatus.Pending,
            AppTimeZones.LocalDateAndTimeToUtc(AppTimeZones.TodayLocalDate().AddDays(1), timeOfDay));

        var result = await Book.HandleAsync(BookCommand(client, behaviorCase, specialist));

        Assert.Equal(BookBehaviorSessionOutcome.NoSlot, result.Outcome);
        Assert.Empty(_db.PaymentTransactions);
    }

    [Fact]
    public async Task Saving_a_follow_up_activates_the_plan_and_notifies_the_client()
    {
        var client = TestData.AddUser(_db);
        var behaviorCase = AddCase(client, BehaviorCaseStatus.Scheduled, TestData.AddPet(_db, client));

        var saved = await new SaveBehaviorFollowUpHandler(_db).HandleAsync(
            new SaveBehaviorFollowUpCommand(client.Id, behaviorCase.Id, "  Mejoró en casa  "));

        Assert.Equal("Mejoró en casa", saved!.FollowUpNotes);
        Assert.Equal(BehaviorCaseStatus.PlanActive, Reload(behaviorCase.Id).Status);
        Assert.Contains(_db.Notifications, n => n.UserId == client.Id && n.Type == "behavior-plan");
        Assert.Null(await new SaveBehaviorFollowUpHandler(_db).HandleAsync(
            new SaveBehaviorFollowUpCommand(TestData.AddUser(_db).Id, behaviorCase.Id, "x")));
    }
}
