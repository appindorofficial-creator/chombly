using WebAppPet.Application.Payments.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Payments;

public class ProviderPayoutServiceTests
{
    private static readonly DateTime PeriodStart = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodEnd = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private static ProviderPayoutService Service(AppDbContext db) => new(db, new VetAuditService(db));

    private static Appointment AddPaidBooking(AppDbContext db, GroomerProfile business, decimal total, DateTime createdAt,
        AppointmentStatus status = AppointmentStatus.Confirmed)
    {
        var appt = TestData.AddAppointment(db, TestData.AddUser(db), business, status, createdAt.AddDays(2));
        appt.TotalPrice = total;
        appt.CreatedAt = createdAt;
        db.SaveChanges();
        return appt;
    }

    [Fact]
    public async Task Charges_the_platform_default_commission_on_bookings_created_in_the_period()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var business = TestData.AddBusiness(db);
        await Service(db).SeedDefaultRulesAsync();
        AddPaidBooking(db, business, 40, PeriodStart.AddDays(3));
        AddPaidBooking(db, business, 60, PeriodStart.AddDays(10));
        AddPaidBooking(db, business, 500, PeriodStart.AddDays(5), AppointmentStatus.Cancelled);
        AddPaidBooking(db, business, 700, PeriodEnd.AddDays(1));

        var calc = await Service(db).CalculatePayoutForPeriodAsync(business.UserId, PeriodStart, PeriodEnd);

        Assert.Equal(100m, calc.Gross);
        Assert.Equal(20m, calc.Commission);
        Assert.Equal(80m, calc.Net);
        Assert.Equal(2, calc.Count);
        Assert.Equal(20m, calc.Rule!.CommissionPercent);
    }

    [Fact]
    public async Task A_provider_rule_with_flat_fee_beats_the_platform_default()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var business = TestData.AddBusiness(db);
        await Service(db).SeedDefaultRulesAsync();
        db.ProviderCompensationRules.Add(new ProviderCompensationRule
        {
            ProviderUserId = business.UserId,
            ServiceType = CompensationServiceType.LocalVet,
            CommissionPercent = 10,
            FlatFeeUsd = 5,
            EffectiveFrom = PeriodStart.AddDays(-1)
        });
        db.SaveChanges();
        AddPaidBooking(db, business, 100, PeriodStart.AddDays(3));

        var calc = await Service(db).CalculatePayoutForPeriodAsync(business.UserId, PeriodStart, PeriodEnd);

        Assert.Equal(15m, calc.Commission);
        Assert.Equal(85m, calc.Net);
    }

    [Fact]
    public async Task Without_any_rule_the_commission_falls_back_to_twenty_percent()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var business = TestData.AddBusiness(db);
        AddPaidBooking(db, business, 50, PeriodStart.AddDays(3));

        var calc = await Service(db).CalculatePayoutForPeriodAsync(business.UserId, PeriodStart, PeriodEnd);

        Assert.Null(calc.Rule);
        Assert.Equal(10m, calc.Commission);
    }

    [Fact]
    public async Task Creates_a_pending_summary_and_rejects_an_overlapping_one_with_items()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var business = TestData.AddBusiness(db);
        AddPaidBooking(db, business, 50, PeriodStart.AddDays(3));

        var payout = await Service(db).CreatePendingPayoutAsync(business.UserId, PeriodStart, PeriodEnd);

        Assert.Equal(ProviderPayoutStatus.Pending, payout.Status);
        Assert.Equal(40m, payout.NetAmountUsd);
        Assert.Equal(1, payout.ConsultationCount);
        Assert.Null(payout.Notes);
        await Assert.ThrowsAsync<PayoutPeriodOverlapException>(() =>
            Service(db).CreatePendingPayoutAsync(business.UserId, PeriodStart.AddDays(5), PeriodEnd.AddDays(5)));
    }

    [Fact]
    public async Task An_empty_pending_summary_is_replaced_by_a_new_one()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var business = TestData.AddBusiness(db);
        var empty = await Service(db).CreatePendingPayoutAsync(business.UserId, PeriodStart, PeriodEnd);
        Assert.Equal("No completed billable items in period (simulated summary).", empty.Notes);

        var replacement = await Service(db).CreatePendingPayoutAsync(business.UserId, PeriodStart, PeriodEnd);

        Assert.Equal(replacement.Id, Assert.Single(db.ProviderPayouts).Id);
    }

    [Fact]
    public async Task Marking_paid_refreshes_totals_and_sets_a_simulated_reference_once()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var business = TestData.AddBusiness(db);
        var payout = await Service(db).CreatePendingPayoutAsync(business.UserId, PeriodStart, PeriodEnd);
        AddPaidBooking(db, business, 50, PeriodStart.AddDays(3));

        var paid = await Service(db).MarkPaidAsync(payout.Id);

        Assert.Equal(ProviderPayoutStatus.Paid, paid!.Status);
        Assert.NotNull(paid.PaidUtc);
        Assert.StartsWith("sim_connect_", paid.ExternalReference);
        Assert.Equal(50m, paid.GrossAmountUsd);
        Assert.Equal(40m, paid.NetAmountUsd);
        Assert.Null(paid.Notes);

        var again = await Service(db).MarkPaidAsync(payout.Id);
        Assert.Equal(paid.ExternalReference, again!.ExternalReference);
        Assert.Null(await Service(db).MarkPaidAsync(9999));
    }

    [Fact]
    public async Task Refreshing_totals_leaves_paid_summaries_as_they_were_settled()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var business = TestData.AddBusiness(db);
        var paid = await Service(db).CreatePendingPayoutAsync(business.UserId, PeriodStart, PeriodEnd);
        await Service(db).MarkPaidAsync(paid.Id);
        var pending = await Service(db).CreatePendingPayoutAsync(business.UserId, PeriodEnd, PeriodEnd.AddDays(30));
        AddPaidBooking(db, business, 50, PeriodStart.AddDays(3));
        AddPaidBooking(db, business, 80, PeriodEnd.AddDays(3));

        Assert.Equal(1, await Service(db).RefreshPayoutTotalsAsync(business.UserId));
        Assert.Equal(0, await Service(db).RefreshAllPayoutTotalsAsync());

        using var check = database.CreateContext();
        var settled = check.ProviderPayouts.Single(p => p.Id == paid.Id);
        Assert.Equal(0m, settled.GrossAmountUsd);
        Assert.Equal(0, settled.ConsultationCount);
        Assert.Equal(80m, check.ProviderPayouts.Single(p => p.Id == pending.Id).GrossAmountUsd);
    }

    [Fact]
    public async Task Provider_rule_on_onboarding_copies_the_default_in_the_local_currency()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var business = TestData.AddBusiness(db);
        db.Users.Find(business.UserId)!.CountryCode = "CO";
        db.SaveChanges();
        await Service(db).SeedDefaultRulesAsync();

        await Service(db).EnsureProviderRuleAsync(business.UserId, CompensationServiceType.InternationalVet);
        await Service(db).EnsureProviderRuleAsync(business.UserId, CompensationServiceType.InternationalVet);

        var rule = Assert.Single(await Service(db).ListRulesForProviderAsync(business.UserId));
        Assert.Equal(25m, rule.CommissionPercent);
        Assert.Equal("COP", rule.PayoutCurrency);
    }

    [Fact]
    public async Task Recent_family_payments_show_commission_and_net_per_booking()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var business = TestData.AddBusiness(db);
        await Service(db).SeedDefaultRulesAsync();
        AddPaidBooking(db, business, 45.55m, PeriodStart.AddDays(3));
        AddPaidBooking(db, business, 99, PeriodStart.AddDays(4), AppointmentStatus.Cancelled);

        var row = Assert.Single(await Service(db).ListRecentFamilyPaymentsAsync(business.UserId));

        Assert.Equal(45.55m, row.Gross);
        Assert.Equal(9.11m, row.Commission);
        Assert.Equal(36.44m, row.Net);
        Assert.Equal("Thor", row.PetName);
    }
}
