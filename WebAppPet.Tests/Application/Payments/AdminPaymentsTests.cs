using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Common;
using WebAppPet.Application.Payments.GetAdminPayments;
using WebAppPet.Application.Payments.RefundPayment;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;
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
    public async Task Each_held_business_charge_shows_its_commission_and_the_rest_do_not()
    {
        var business = TestData.AddBusiness(_db);
        var held = TestData.AddCharge(_db, business, 35m, Day, serviceTotal: 100m);
        var refunded = TestData.AddCharge(_db, business, 14m, Day, serviceTotal: 40m, status: PaymentTransactionStatus.Refunded);
        var care = TestData.AddCharge(_db, null, 14.99m, Day, purpose: PaymentPurpose.CareSubscription);

        var rows = (await Handler().HandleAsync(new GetAdminPaymentsQuery())).Rows.ToDictionary(r => r.Id);

        Assert.Equal((20m, 20m), (rows[held.Id].CommissionPercent, rows[held.Id].Commission));
        Assert.Equal((20m, (decimal?)null), (rows[refunded.Id].CommissionPercent, rows[refunded.Id].Commission));
        Assert.Equal(((decimal?)null, (decimal?)null), (rows[care.Id].CommissionPercent, rows[care.Id].Commission));
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

    private Task<Result> Refund(PaymentTransaction charge) =>
        new RefundPaymentHandler(_db, TestData.Payments(_db)).HandleAsync(new RefundPaymentCommand(charge.Id, null, ""));

    [Theory]
    [InlineData(AppointmentStatus.Pending)]
    [InlineData(AppointmentStatus.Confirmed)]
    public async Task Refunding_an_upcoming_appointment_cancels_it_and_tells_the_family_and_the_business(AppointmentStatus status)
    {
        var client = TestData.AddUser(_db);
        var business = TestData.AddBusiness(_db);
        var appointment = TestData.AddAppointment(_db, client, business, status, DateTime.UtcNow.AddDays(2));
        var charge = TestData.AddCharge(_db, business, 40m, Day, appointment: appointment);

        await Refund(charge);

        using var db = _database.CreateContext();
        Assert.Equal(AppointmentStatus.Cancelled, db.Appointments.AsNoTracking().Single().Status);
        Assert.Equal(
            [(client.Id, "appointment"), (business.UserId, "appointment")],
            db.Notifications.AsNoTracking().OrderBy(n => n.UserId).Select(n => new { n.UserId, n.Type }).AsEnumerable()
                .Select(n => (n.UserId, n.Type)));
    }

    [Fact]
    public async Task Refunding_a_completed_appointment_keeps_it_and_only_tells_the_family()
    {
        var client = TestData.AddUser(_db);
        var business = TestData.AddBusiness(_db);
        var appointment = TestData.AddAppointment(_db, client, business, AppointmentStatus.Completed, DateTime.UtcNow.AddDays(-2));
        var charge = TestData.AddCharge(_db, business, 40m, Day, appointment: appointment);

        await Refund(charge);

        using var db = _database.CreateContext();
        Assert.Equal(AppointmentStatus.Completed, db.Appointments.AsNoTracking().Single().Status);
        Assert.Equal(client.Id, db.Notifications.AsNoTracking().Single().UserId);
    }

    [Fact]
    public async Task Refunding_a_care_charge_ends_the_membership()
    {
        var client = TestData.AddUser(_db);
        var subscription = new CareSubscription { UserId = client.Id };
        _db.CareSubscriptions.Add(subscription);
        _db.SaveChanges();
        var charge = TestData.AddCharge(_db, null, 50_000m, Day, purpose: PaymentPurpose.CareSubscription);
        charge.UserId = client.Id;
        charge.CareSubscriptionId = subscription.Id;
        _db.SaveChanges();

        await Refund(charge);

        using var db = _database.CreateContext();
        Assert.Equal(CareSubscriptionStatus.Cancelled, db.CareSubscriptions.AsNoTracking().Single().Status);
        var notice = db.Notifications.AsNoTracking().Single();
        Assert.Equal((client.Id, "care"), (notice.UserId, notice.Type));
        Assert.Contains("$50.000", notice.Message);
    }

    [Fact]
    public async Task A_failed_refund_leaves_the_appointment_alone()
    {
        var client = TestData.AddUser(_db);
        var business = TestData.AddBusiness(_db);
        var appointment = TestData.AddAppointment(_db, client, business, AppointmentStatus.Confirmed, DateTime.UtcNow.AddDays(2));
        var charge = TestData.AddCharge(_db, business, 40m, Day, status: PaymentTransactionStatus.Failed, appointment: appointment);

        var result = await Refund(charge);

        Assert.False(result.Success);
        using var db = _database.CreateContext();
        Assert.Equal(AppointmentStatus.Confirmed, db.Appointments.AsNoTracking().Single().Status);
        Assert.Empty(db.Notifications.AsNoTracking());
    }
}
