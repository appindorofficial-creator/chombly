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

    [Fact]
    public async Task Settles_deposits_collected_in_the_period_with_commission_on_the_service_total()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var business = TestData.AddBusiness(db);
        await Service(db).SeedDefaultRulesAsync();
        TestData.AddCharge(db, business, 15, PeriodStart.AddDays(3), serviceTotal: 40);
        TestData.AddCharge(db, business, 21, PeriodStart.AddDays(10), serviceTotal: 60);
        TestData.AddCharge(db, business, 175, PeriodStart.AddDays(5), serviceTotal: 500, status: PaymentTransactionStatus.Refunded);
        TestData.AddCharge(db, business, 175, PeriodStart.AddDays(6), serviceTotal: 500, status: PaymentTransactionStatus.Failed);
        TestData.AddCharge(db, business, 245, PeriodEnd.AddDays(1), serviceTotal: 700);
        TestData.AddCharge(db, TestData.AddBusiness(db), 99, PeriodStart.AddDays(3));

        var calc = await Service(db).CalculatePayoutForPeriodAsync(business.UserId, PeriodStart, PeriodEnd);

        Assert.Equal(36m, calc.Gross);
        Assert.Equal(20m, calc.Commission);
        Assert.Equal(16m, calc.Net);
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
        TestData.AddCharge(db, business, 35, PeriodStart.AddDays(3), serviceTotal: 100);

        var calc = await Service(db).CalculatePayoutForPeriodAsync(business.UserId, PeriodStart, PeriodEnd);

        Assert.Equal(15m, calc.Commission);
        Assert.Equal(20m, calc.Net);
    }

    [Fact]
    public async Task Without_any_rule_the_commission_falls_back_to_twenty_percent()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var business = TestData.AddBusiness(db);
        TestData.AddCharge(db, business, 50, PeriodStart.AddDays(3));

        var calc = await Service(db).CalculatePayoutForPeriodAsync(business.UserId, PeriodStart, PeriodEnd);

        Assert.Null(calc.Rule);
        Assert.Equal(10m, calc.Commission);
    }

    [Fact]
    public async Task A_period_without_charges_owes_nothing_even_with_a_flat_fee()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var business = TestData.AddBusiness(db);
        db.ProviderCompensationRules.Add(new ProviderCompensationRule
        {
            ProviderUserId = business.UserId,
            ServiceType = CompensationServiceType.LocalVet,
            CommissionPercent = 10,
            FlatFeeUsd = 5,
            IsActive = true,
            EffectiveFrom = PeriodStart.AddDays(-1)
        });
        db.SaveChanges();

        var calc = await Service(db).CalculatePayoutForPeriodAsync(business.UserId, PeriodStart, PeriodEnd);

        Assert.Equal((0m, 0m, 0m, 0), (calc.Gross, calc.Commission, calc.Net, calc.Count));
    }

    [Fact]
    public async Task Creates_a_pending_summary_and_rejects_an_overlapping_one_with_items()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var business = TestData.AddBusiness(db);
        TestData.AddCharge(db, business, 50, PeriodStart.AddDays(3));

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
        TestData.AddCharge(db, business, 50, PeriodStart.AddDays(3));

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
    public async Task A_refund_before_settlement_drops_the_charge_from_the_pending_summary()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var business = TestData.AddBusiness(db);
        var charge = TestData.AddCharge(db, business, 50, PeriodStart.AddDays(3));
        var payout = await Service(db).CreatePendingPayoutAsync(business.UserId, PeriodStart, PeriodEnd);

        await TestData.Payments(db).RefundAsync(charge, "client_cancelled", null);
        await Service(db).RefreshPayoutTotalsAsync(business.UserId);

        using var check = database.CreateContext();
        var refreshed = check.ProviderPayouts.Single(p => p.Id == payout.Id);
        Assert.Equal((0m, 0), (refreshed.GrossAmountUsd, refreshed.ConsultationCount));
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
        TestData.AddCharge(db, business, 50, PeriodStart.AddDays(3));
        TestData.AddCharge(db, business, 80, PeriodEnd.AddDays(3));

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
    public async Task Recent_customer_payments_show_deposit_balance_commission_and_net()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var business = TestData.AddBusiness(db);
        await Service(db).SeedDefaultRulesAsync();
        var appt = TestData.AddAppointment(db, TestData.AddUser(db), business, AppointmentStatus.Confirmed, PeriodStart.AddDays(5));
        TestData.AddCharge(db, business, 15.94m, PeriodStart.AddDays(3), serviceTotal: 45.55m, appointment: appt);
        TestData.AddCharge(db, business, 20, PeriodStart.AddDays(4), status: PaymentTransactionStatus.Refunded);
        TestData.AddCharge(db, business, 30, PeriodStart.AddDays(5), status: PaymentTransactionStatus.Failed);

        var rows = await Service(db).ListRecentFamilyPaymentsAsync(business.UserId);

        Assert.Equal(2, rows.Count);
        var refunded = rows[0];
        Assert.Equal((PaymentTransactionStatus.Refunded, 0m, 0m), (refunded.Status, refunded.Commission, refunded.Net));
        var deposit = rows[1];
        Assert.Equal(15.94m, deposit.ChargedOnline);
        Assert.Equal(45.55m, deposit.ServiceTotal);
        Assert.Equal(29.61m, deposit.BalanceAtBusiness);
        Assert.Equal(9.11m, deposit.Commission);
        Assert.Equal(6.83m, deposit.Net);
        Assert.Equal("Thor", deposit.PetName);
        Assert.Equal(appt.ScheduledAt, deposit.ScheduledAtUtc);
    }
}
