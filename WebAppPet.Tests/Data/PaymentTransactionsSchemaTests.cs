using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Data;

public class PaymentTransactionsSchemaTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly AppUser _client;
    private readonly GroomerProfile _business;

    public PaymentTransactionsSchemaTests()
    {
        _db = _database.CreateContext();
        _client = TestData.AddUser(_db);
        _client.CountryCode = "US";
        _db.SaveChanges();
        _business = TestData.AddBusiness(_db);
        _db.Database.ExecuteSqlRaw("DROP TABLE \"PaymentTransactions\"");
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private Appointment AddBooking(AppointmentStatus status, decimal total, decimal deposit, string? notes = null)
    {
        var appt = TestData.AddAppointment(_db, _client, _business, status, DateTime.UtcNow.AddDays(3));
        appt.TotalPrice = total;
        appt.DepositPaid = deposit;
        appt.Notes = notes;
        appt.CreatedAt = new DateTime(2026, 9, 5, 10, 0, 0, DateTimeKind.Utc);
        _db.SaveChanges();
        return appt;
    }

    private List<PaymentTransaction> Transactions()
    {
        using var db = _database.CreateContext();
        return db.PaymentTransactions.AsNoTracking().OrderBy(t => t.Id).ToList();
    }

    [Fact]
    public async Task Creating_the_table_records_earlier_charges_as_legacy_transactions()
    {
        var booking = AddBooking(AppointmentStatus.Confirmed, 40m, 15m);
        var cancelled = AddBooking(AppointmentStatus.Cancelled, 60m, 21m);
        var vet = AddBooking(AppointmentStatus.Pending, 25m, 25m);
        var consultation = new Consultation { ClientId = _client.Id, AppointmentId = vet.Id, ServiceCatalogCode = "vet_intl_30" };
        _db.Consultations.Add(consultation);
        var behavior = AddBooking(AppointmentStatus.Confirmed, 50m, 50m, "Behavior case #7 · session 1");
        AddBooking(AppointmentStatus.Confirmed, 0m, 0m);
        var care = new CareSubscription { UserId = _client.Id, PricePerMonth = 14.99m, StartedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) };
        _db.CareSubscriptions.Add(care);
        _db.SaveChanges();

        await PaymentTransactionsSchema.EnsureAsync(_db);

        var rows = Transactions();
        Assert.Equal(5, rows.Count);
        Assert.All(rows, t => Assert.Equal(PaymentTransactionsSchema.LegacyGateway, t.Gateway));
        Assert.All(rows, t => Assert.Equal("USD", t.Currency));

        var deposit = rows.Single(t => t.AppointmentId == booking.Id);
        Assert.Equal((PaymentPurpose.BookingDeposit, PaymentTransactionStatus.Succeeded, 15m, 40m, _business.Id),
            (deposit.Purpose, deposit.Status, deposit.Amount, deposit.ServiceTotal, deposit.ProviderId));
        Assert.Equal($"legacy_appt_{booking.Id}", deposit.ExternalReference);
        Assert.Equal(booking.CreatedAt, deposit.CreatedAt);

        var refunded = rows.Single(t => t.AppointmentId == cancelled.Id);
        Assert.Equal(PaymentTransactionStatus.Refunded, refunded.Status);
        Assert.Equal($"legacy_refund_{cancelled.Id}", refunded.RefundReference);

        var consult = rows.Single(t => t.AppointmentId == vet.Id);
        Assert.Equal((PaymentPurpose.VetConsultation, consultation.Id), (consult.Purpose, consult.ConsultationId));

        var session = rows.Single(t => t.AppointmentId == behavior.Id);
        Assert.Equal((PaymentPurpose.BehaviorSession, 7), (session.Purpose, session.BehaviorCaseId));

        var monthly = rows.Single(t => t.Purpose == PaymentPurpose.CareSubscription);
        Assert.Equal((care.Id, 14.99m, (int?)null, care.StartedAt), (monthly.CareSubscriptionId!.Value, monthly.Amount, monthly.ProviderId, monthly.CreatedAt));
    }

    [Fact]
    public async Task The_backfill_runs_only_when_the_table_is_created()
    {
        AddBooking(AppointmentStatus.Confirmed, 40m, 15m);
        await PaymentTransactionsSchema.EnsureAsync(_db);
        AddBooking(AppointmentStatus.Confirmed, 80m, 28m);

        await PaymentTransactionsSchema.EnsureAsync(_db);

        Assert.Single(Transactions());
    }
}
