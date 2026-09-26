using WebAppPet.Application.Payments.GeneratePayout;
using WebAppPet.Application.Payments.GetAdminPayouts;
using WebAppPet.Application.Payments.GetProviderPayouts;
using WebAppPet.Application.Payments.MarkPayoutPaid;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Payments;

public class PayoutHandlersTests
{
    private static readonly DateTime PeriodStart = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodEnd = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private static ProviderPayoutService Service(AppDbContext db) => new(db, new VetAuditService(db));

    [Fact]
    public async Task Generating_rejects_a_period_that_ends_before_it_starts()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var business = TestData.AddBusiness(db);

        var result = await new GeneratePayoutHandler(Service(db))
            .HandleAsync(new GeneratePayoutCommand(business.UserId, PeriodEnd, PeriodStart));

        Assert.Equal(GeneratePayoutError.InvalidPeriod, result.Error);
        Assert.Empty(db.ProviderPayouts);
    }

    [Fact]
    public async Task Generating_reports_an_overlap_with_a_summary_that_has_items()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var business = TestData.AddBusiness(db);
        TestData.AddCharge(db, business, 40, PeriodStart.AddDays(3));
        var handler = new GeneratePayoutHandler(Service(db));
        await handler.HandleAsync(new GeneratePayoutCommand(business.UserId, PeriodStart, PeriodEnd));

        var result = await handler.HandleAsync(
            new GeneratePayoutCommand(business.UserId, PeriodStart.AddDays(2), PeriodEnd.AddDays(2)));

        Assert.Equal(GeneratePayoutError.PeriodOverlap, result.Error);
        Assert.Null(result.Payout);
        Assert.Single(db.ProviderPayouts);
    }

    [Fact]
    public async Task Generating_creates_a_pending_payout_logged_as_the_provider()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var business = TestData.AddBusiness(db);

        var result = await new GeneratePayoutHandler(Service(db))
            .HandleAsync(new GeneratePayoutCommand(business.UserId, PeriodStart, PeriodEnd));

        Assert.True(result.Success);
        Assert.Equal(ProviderPayoutStatus.Pending, result.Payout!.Status);
        Assert.Equal(business.UserId, Assert.Single(db.AuditLogs).ActorUserId);
    }

    [Fact]
    public async Task The_admin_view_refreshes_totals_only_when_asked()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var business = TestData.AddBusiness(db);
        var payout = await Service(db).CreatePendingPayoutAsync(business.UserId, PeriodStart, PeriodEnd);
        TestData.AddCharge(db, business, 40, PeriodStart.AddDays(2));
        var handler = new GetAdminPayoutsHandler(Service(db));

        var plain = await handler.HandleAsync(new GetAdminPayoutsQuery(RefreshTotals: false));
        Assert.Equal(0, plain.Refreshed);
        Assert.Equal(0m, Assert.Single(plain.Pending).GrossAmountUsd);

        var refreshed = await handler.HandleAsync(new GetAdminPayoutsQuery(RefreshTotals: true));
        Assert.Equal(1, refreshed.Refreshed);
        Assert.Equal(40m, Assert.Single(refreshed.Pending).GrossAmountUsd);
        Assert.Equal(payout.Id, Assert.Single(refreshed.Recent).Id);
    }

    [Fact]
    public async Task A_paid_payout_leaves_the_pending_list()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var business = TestData.AddBusiness(db);
        var admin = TestData.AddUser(db, UserRole.Admin);
        var payout = await Service(db).CreatePendingPayoutAsync(business.UserId, PeriodStart, PeriodEnd);

        await new MarkPayoutPaidHandler(Service(db)).HandleAsync(new MarkPayoutPaidCommand(payout.Id, admin.Id));

        var view = await new GetAdminPayoutsHandler(Service(db)).HandleAsync(new GetAdminPayoutsQuery(false));
        Assert.Empty(view.Pending);
        Assert.Equal(ProviderPayoutStatus.Paid, Assert.Single(view.Recent).Status);
    }

    [Fact]
    public async Task The_provider_view_includes_rules_history_and_recent_bookings()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var business = TestData.AddBusiness(db);
        await Service(db).SeedDefaultRulesAsync();
        await Service(db).CreatePendingPayoutAsync(business.UserId, PeriodStart, PeriodEnd);
        TestData.AddCharge(db, business, 14, PeriodStart.AddDays(4), serviceTotal: 40);

        var view = await new GetProviderPayoutsHandler(Service(db))
            .HandleAsync(new GetProviderPayoutsQuery(business.UserId));

        Assert.Equal(3, view.Rules.Count);
        Assert.Single(view.History);
        Assert.Single(view.RecentPayments);
    }
}
