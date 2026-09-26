using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Payments.GetAdminPayments;
using WebAppPet.Application.Payments.RefundPayment;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Payments;

public class AdminPaymentsTests : IDisposable
{
    private static readonly DateTime Day = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;

    public AdminPaymentsTests() => _db = _database.CreateContext();

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private GetAdminPaymentsHandler Handler() => new(_db, new ProviderPayoutService(_db, new VetAuditService(_db)));

    [Fact]
    public async Task Totals_split_collected_refunded_declined_commission_and_own_revenue_per_currency()
    {
        var business = TestData.AddBusiness(_db);
        TestData.AddCharge(_db, business, 35m, Day, serviceTotal: 100m);
        TestData.AddCharge(_db, business, 14m, Day, serviceTotal: 40m, status: PaymentTransactionStatus.Refunded);
        TestData.AddCharge(_db, business, 14m, Day, status: PaymentTransactionStatus.Failed);
        TestData.AddCharge(_db, null, 14.99m, Day, purpose: PaymentPurpose.CareSubscription);
        TestData.AddCharge(_db, business, 10m, Day, currency: "USD");

        var view = await Handler().HandleAsync(new GetAdminPaymentsQuery());

        Assert.Equal(5, view.MatchingCount);
        Assert.Equal(5, view.Rows.Count);
        var cop = view.Totals.Single(t => t.Currency == "COP");
        Assert.Equal(49.99m, cop.Collected);
        Assert.Equal(14m, cop.Refunded);
        Assert.Equal(1, cop.FailedCount);
        Assert.Equal(20m, cop.Commission);
        Assert.Equal(14.99m, cop.OwnRevenue);
        Assert.Equal(10m, view.Totals.Single(t => t.Currency == "USD").Collected);
    }

    [Fact]
    public async Task Filters_by_status_purpose_and_date_range()
    {
        var business = TestData.AddBusiness(_db);
        var inRange = TestData.AddCharge(_db, business, 15m, Day);
        TestData.AddCharge(_db, business, 15m, Day, status: PaymentTransactionStatus.Failed);
        TestData.AddCharge(_db, business, 15m, Day, purpose: PaymentPurpose.VetConsultation);
        TestData.AddCharge(_db, business, 15m, Day.AddDays(-5));

        var view = await Handler().HandleAsync(new GetAdminPaymentsQuery(
            PaymentTransactionStatus.Succeeded, PaymentPurpose.BookingDeposit, Day.AddDays(-1), Day.AddDays(1)));

        var row = Assert.Single(view.Rows);
        Assert.Equal(inRange.Id, row.Id);
        Assert.Equal(business.BusinessName, row.BusinessName);
        Assert.Equal(1, view.MatchingCount);
    }

    [Fact]
    public async Task Admin_refund_marks_the_charge_and_rejects_a_second_one()
    {
        var charge = TestData.AddCharge(_db, TestData.AddBusiness(_db), 15m, Day);
        var admin = TestData.AddUser(_db, UserRole.Admin);
        var handler = new RefundPaymentHandler(_db, TestData.Payments(_db));

        var first = await handler.HandleAsync(new RefundPaymentCommand(charge.Id, admin.Id, "  duplicate  "));
        var second = await handler.HandleAsync(new RefundPaymentCommand(charge.Id, admin.Id, ""));
        var missing = await handler.HandleAsync(new RefundPaymentCommand(9999, admin.Id, ""));

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.False(missing.Success);
        using var db = _database.CreateContext();
        var saved = db.PaymentTransactions.AsNoTracking().Single();
        Assert.Equal((PaymentTransactionStatus.Refunded, "duplicate"), (saved.Status, saved.RefundReason));
        Assert.Equal(admin.Id, db.AuditLogs.AsNoTracking().Single(a => a.Action == "payment_refunded").ActorUserId);
    }
}
