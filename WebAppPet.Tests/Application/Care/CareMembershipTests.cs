using WebAppPet.Application.Care.ActivateCare;
using WebAppPet.Application.Care.CancelCare;
using WebAppPet.Application.Care.GetCarePlan;
using WebAppPet.Application.Care.GetPetCare;
using WebAppPet.Application.Care.Shared;
using WebAppPet.Application.Common;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Care;

public class CareMembershipTests : IDisposable
{
    private const int MonthlyPrice = 20;

    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly AppUser _user;

    public CareMembershipTests()
    {
        _db = _database.CreateContext();
        _user = TestData.AddUser(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private ChomblyCareService Care => new(_db);
    private GetCarePlanHandler GetPlan => new(new ServiceCatalogService(_db), Care, TestData.Payments(_db));
    private ActivateCareHandler Activate =>
        new(_db, new ServiceCatalogService(_db), Care, TestData.Payments(_db), new ConsentService(_db), new VetAuditService(_db));

    private void AddCatalogPrice()
    {
        _db.ServiceCatalog.Add(new ServiceCatalogItem
        {
            Code = ServiceCatalogCodes.ChomblyCare,
            NameEs = "Chombly Care",
            ScopeEs = "Plan mensual",
            Price = MonthlyPrice,
            IsBookable = false
        });
        _db.SaveChanges();
    }

    private Task<ActivateCareResult> ActivateAs(AppUser user, bool acceptTerms = true, bool acceptRenewal = true, int? consultationId = null) =>
        Activate.HandleAsync(new ActivateCareCommand(user.Id, consultationId, acceptTerms, acceptRenewal, "127.0.0.1", "tests"));

    private CareSubscription AddSubscription(DateTime periodEnd, bool cancelAtPeriodEnd = false)
    {
        var subscription = new CareSubscription
        {
            UserId = _user.Id,
            CurrentPeriodStart = periodEnd.AddMonths(-1),
            CurrentPeriodEnd = periodEnd,
            CancelAtPeriodEnd = cancelAtPeriodEnd
        };
        _db.CareSubscriptions.Add(subscription);
        _db.SaveChanges();
        return subscription;
    }

    [Fact]
    public async Task Activating_charges_the_first_month_and_links_the_charge()
    {
        AddCatalogPrice();
        TestData.AddCard(_db, _user);

        var result = await ActivateAs(_user);

        Assert.Equal(ActivateCareOutcome.Activated, result.Outcome);
        using var verify = _database.CreateContext();
        var subscription = verify.CareSubscriptions.Single();
        Assert.Equal((_user.Id, CareSubscriptionStatus.Active, CarePlan.ColombiaMonthlyPrice, 1),
            (subscription.UserId, subscription.Status, subscription.PricePerMonth, subscription.QuickConsultsPerCycle));
        Assert.Equal(subscription.CurrentPeriodStart.AddMonths(1), subscription.CurrentPeriodEnd);
        var charge = verify.PaymentTransactions.Single();
        Assert.Equal((PaymentPurpose.CareSubscription, PaymentTransactionStatus.Succeeded, CarePlan.ColombiaMonthlyPrice, subscription.Id),
            (charge.Purpose, charge.Status, charge.Amount, charge.CareSubscriptionId));
    }

    [Theory]
    [InlineData(BusinessMarket.Colombia, 50_000, "COP")]
    [InlineData(BusinessMarket.UnitedStates, 20, "USD")]
    public async Task Colombia_pays_the_peso_price_and_the_United_States_the_catalog_price(
        BusinessMarket market, int expectedPrice, string currency)
    {
        AddCatalogPrice();
        TestData.AddCard(_db, _user);

        using (AppTimeZones.UseMarket(market))
            await ActivateAs(_user);

        using var verify = _database.CreateContext();
        var charge = verify.PaymentTransactions.Single();
        Assert.Equal(((decimal)expectedPrice, currency), (charge.Amount, charge.Currency));
        Assert.Equal(expectedPrice, verify.CareSubscriptions.Single().PricePerMonth);
    }

    [Theory]
    [InlineData(BusinessMarket.Colombia, "COP")]
    [InlineData(BusinessMarket.UnitedStates, "USD")]
    public async Task The_membership_is_recorded_in_the_currency_it_was_charged_in(BusinessMarket market, string currency)
    {
        TestData.AddCard(_db, _user);

        using (AppTimeZones.UseMarket(market))
            await ActivateAs(_user);

        using var verify = _database.CreateContext();
        Assert.Equal(currency, verify.PaymentTransactions.Single().Currency);
        Assert.Equal(currency, verify.CareSubscriptions.Single().Currency);
    }

    [Fact]
    public async Task Activating_records_both_consents_and_an_audit_entry()
    {
        TestData.AddCard(_db, _user);

        await ActivateAs(_user);

        using var verify = _database.CreateContext();
        Assert.Equal(
            new[] { "care_auto_renewal", ConsentService.DocTerms }.Order(),
            verify.ConsentRecords.Where(c => c.UserId == _user.Id).Select(c => c.DocumentKey).AsEnumerable().Order());
        Assert.Contains(verify.AuditLogs, a => a.Action == "care_activated");
    }

    [Fact]
    public async Task Without_a_catalog_price_the_default_monthly_price_is_charged_in_the_United_States()
    {
        TestData.AddCard(_db, _user);

        using (AppTimeZones.UseMarket(BusinessMarket.UnitedStates))
            await ActivateAs(_user);

        using var verify = _database.CreateContext();
        Assert.Equal(CarePlan.FallbackMonthlyPrice, verify.PaymentTransactions.Single().Amount);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Both_terms_and_auto_renewal_must_be_accepted(bool acceptTerms, bool acceptRenewal)
    {
        TestData.AddCard(_db, _user);

        var result = await ActivateAs(_user, acceptTerms, acceptRenewal);

        Assert.Equal(ActivateCareOutcome.TermsNotAccepted, result.Outcome);
        using var verify = _database.CreateContext();
        Assert.Empty(verify.CareSubscriptions);
        Assert.Empty(verify.PaymentTransactions);
    }

    [Fact]
    public async Task Without_a_card_nothing_is_activated()
    {
        var result = await ActivateAs(_user);

        Assert.Equal(ActivateCareOutcome.NoPaymentMethod, result.Outcome);
        using var verify = _database.CreateContext();
        Assert.Empty(verify.CareSubscriptions);
    }

    [Fact]
    public async Task A_declined_card_leaves_the_family_without_membership()
    {
        TestData.AddCard(_db, _user, last4: "0002");

        var result = await ActivateAs(_user);

        Assert.Equal(ActivateCareOutcome.PaymentDeclined, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.PaymentError));
        using var verify = _database.CreateContext();
        Assert.Empty(verify.CareSubscriptions);
        Assert.Equal(PaymentTransactionStatus.Failed, verify.PaymentTransactions.Single().Status);
    }

    [Fact]
    public async Task Activating_again_while_active_charges_nothing_more()
    {
        TestData.AddCard(_db, _user);
        await ActivateAs(_user);

        var result = await ActivateAs(_user);

        Assert.Equal(ActivateCareOutcome.Activated, result.Outcome);
        using var verify = _database.CreateContext();
        Assert.Single(verify.CareSubscriptions);
        Assert.Single(verify.PaymentTransactions);
    }

    [Fact]
    public async Task Guests_only_see_the_plan()
    {
        AddCatalogPrice();

        var plan = await GetPlan.HandleAsync(new GetCarePlanQuery(null));

        Assert.Equal(MonthlyPrice, plan.CatalogItem?.Price);
        Assert.Null(plan.Subscription);
        Assert.Null(plan.Card);
    }

    [Theory]
    [InlineData(BusinessMarket.Colombia, 50_000)]
    [InlineData(BusinessMarket.UnitedStates, 20)]
    public async Task The_plan_shows_the_monthly_price_of_the_familys_market(BusinessMarket market, int expectedPrice)
    {
        AddCatalogPrice();

        using (AppTimeZones.UseMarket(market))
        {
            var plan = await GetPlan.HandleAsync(new GetCarePlanQuery(_user.Id));

            Assert.Equal(expectedPrice, plan.MonthlyPrice);
        }
    }

    [Fact]
    public async Task Before_subscribing_the_plan_shows_the_card_to_charge()
    {
        TestData.AddCard(_db, _user, last4: "1111", isDefault: false);
        TestData.AddCard(_db, _user, last4: "4242");

        var plan = await GetPlan.HandleAsync(new GetCarePlanQuery(_user.Id));

        Assert.Null(plan.Subscription);
        Assert.Equal("4242", plan.Card?.Last4);
    }

    [Fact]
    public async Task Members_see_their_quick_consults_left_this_cycle()
    {
        var subscription = AddSubscription(DateTime.UtcNow.AddDays(10));
        _db.CareBenefitUses.Add(new CareBenefitUse { SubscriptionId = subscription.Id, PeriodStart = subscription.CurrentPeriodStart });
        _db.SaveChanges();
        TestData.AddCard(_db, _user);

        var plan = await GetPlan.HandleAsync(new GetCarePlanQuery(_user.Id));

        Assert.Equal(subscription.Id, plan.Subscription?.Id);
        Assert.Equal(0, plan.RemainingConsults);
        Assert.Null(plan.Card);
    }

    [Fact]
    public async Task Cancelling_keeps_the_membership_until_the_cycle_ends()
    {
        AddSubscription(DateTime.UtcNow.AddDays(10));

        var cancelled = await new CancelCareHandler(_db, Care).HandleAsync(new CancelCareCommand(_user.Id));

        Assert.True(cancelled);
        using var verify = _database.CreateContext();
        var subscription = verify.CareSubscriptions.Single();
        Assert.True(subscription.CancelAtPeriodEnd);
        Assert.Equal(CareSubscriptionStatus.Active, subscription.Status);
    }

    [Fact]
    public async Task Cancelling_without_a_membership_does_nothing()
    {
        var cancelled = await new CancelCareHandler(_db, Care).HandleAsync(new CancelCareCommand(_user.Id));

        Assert.False(cancelled);
    }

    [Fact]
    public async Task A_cancelled_membership_ends_once_its_cycle_is_over()
    {
        AddSubscription(DateTime.UtcNow.AddDays(-1), cancelAtPeriodEnd: true);

        var plan = await GetPlan.HandleAsync(new GetCarePlanQuery(_user.Id));

        Assert.Null(plan.Subscription);
        using var verify = _database.CreateContext();
        Assert.Equal(CareSubscriptionStatus.Cancelled, verify.CareSubscriptions.Single().Status);
    }

    [Fact]
    public async Task The_pet_care_page_shows_recent_consults_and_upcoming_appointments()
    {
        var business = TestData.AddBusiness(_db);
        var future = TestData.AddAppointment(_db, _user, business, AppointmentStatus.Confirmed, DateTime.UtcNow.AddDays(2));
        var pet = _db.Pets.Single(p => p.Id == future.PetId);
        var cancelled = TestData.AddAppointment(_db, _user, business, AppointmentStatus.Cancelled, DateTime.UtcNow.AddDays(3));
        cancelled.PetId = pet.Id;
        var past = TestData.AddAppointment(_db, _user, business, AppointmentStatus.Completed, DateTime.UtcNow.AddDays(-3));
        past.PetId = pet.Id;
        _db.Consultations.Add(new Consultation { ClientId = _user.Id, PetId = pet.Id, UpdatedAt = DateTime.UtcNow });
        _db.Consultations.Add(new Consultation { ClientId = _user.Id, PetId = TestData.AddPet(_db, _user).Id, UpdatedAt = DateTime.UtcNow });
        _db.SaveChanges();
        AddSubscription(DateTime.UtcNow.AddDays(10));

        var view = await new GetPetCareHandler(_db, Care).HandleAsync(new GetPetCareQuery(_user.Id, pet.Id));

        Assert.NotNull(view);
        Assert.Equal(pet.Id, view.Pet.Id);
        Assert.Equal([future.Id], view.Upcoming.Select(a => a.Id));
        Assert.Single(view.RecentConsults);
        Assert.NotNull(view.Subscription);
        Assert.Equal(1, view.RemainingConsults);
    }

    [Fact]
    public async Task Someone_elses_pet_has_no_care_page()
    {
        var otherPet = TestData.AddPet(_db, TestData.AddUser(_db));

        var view = await new GetPetCareHandler(_db, Care).HandleAsync(new GetPetCareQuery(_user.Id, otherPet.Id));

        Assert.Null(view);
    }
}
