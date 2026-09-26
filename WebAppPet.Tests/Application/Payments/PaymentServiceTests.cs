using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Data;
using WebAppPet.Infrastructure.Payments;
using WebAppPet.Models;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Payments;

public class PaymentServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly AppUser _client;

    public PaymentServiceTests()
    {
        _db = _database.CreateContext();
        _client = TestData.AddUser(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private ChargeRequest Request(PaymentMethod card, decimal amount = 15m) => new()
    {
        UserId = _client.Id,
        Card = card,
        Amount = amount,
        ServiceTotal = 40m,
        Purpose = PaymentPurpose.BookingDeposit,
        Description = "Deposit"
    };

    private List<string> AuditActions()
    {
        using var db = _database.CreateContext();
        return db.AuditLogs.AsNoTracking().OrderBy(a => a.Id).Select(a => a.Action).ToList();
    }

    [Theory]
    [InlineData("0002", "card_declined")]
    [InlineData("9995", "insufficient_funds")]
    [InlineData("0069", "expired_card")]
    [InlineData("0119", "processing_error")]
    public async Task Simulator_declines_the_test_card_endings(string last4, string code)
    {
        var card = new PaymentMethod { Last4 = last4, ExpMonth = 12, ExpYear = 2099 };

        var result = await new SimulatedPaymentGateway().ChargeAsync(new GatewayChargeRequest(10m, "COP", card, "x"));

        Assert.False(result.Succeeded);
        Assert.Equal(code, result.FailureCode);
        Assert.StartsWith("sim_ch_", result.Reference);
    }

    [Fact]
    public async Task Simulator_declines_expired_cards_and_non_positive_amounts()
    {
        var gateway = new SimulatedPaymentGateway();
        var expired = new PaymentMethod { Last4 = "4242", ExpMonth = 1, ExpYear = 2020 };
        var valid = new PaymentMethod { Last4 = "4242", ExpMonth = 12, ExpYear = 2099 };

        Assert.Equal("expired_card", (await gateway.ChargeAsync(new GatewayChargeRequest(10m, "COP", expired, "x"))).FailureCode);
        Assert.Equal("invalid_amount", (await gateway.ChargeAsync(new GatewayChargeRequest(0m, "COP", valid, "x"))).FailureCode);
        Assert.True((await gateway.ChargeAsync(new GatewayChargeRequest(10m, "COP", valid, "x"))).Succeeded);
    }

    [Fact]
    public async Task Approved_charge_is_recorded_with_card_and_audited()
    {
        var card = TestData.AddCard(_db, _client, "4242");

        var charge = await TestData.Payments(_db).ChargeAsync(Request(card));

        using var db = _database.CreateContext();
        var saved = db.PaymentTransactions.AsNoTracking().Single();
        Assert.Equal(charge.Id, saved.Id);
        Assert.Equal(PaymentTransactionStatus.Succeeded, saved.Status);
        Assert.Equal((15m, 40m), (saved.Amount, saved.ServiceTotal));
        Assert.Equal(("Visa", "4242", card.Id), (saved.CardBrand, saved.CardLast4, saved.PaymentMethodId));
        Assert.Equal(SimulatedPaymentGateway.GatewayName, saved.Gateway);
        Assert.False(string.IsNullOrEmpty(saved.Currency));
        Assert.Equal(["payment_succeeded"], AuditActions());
    }

    [Fact]
    public async Task Service_total_defaults_to_the_amount()
    {
        var card = TestData.AddCard(_db, _client);

        var charge = await TestData.Payments(_db).ChargeAsync(Request(card, 30m) with { ServiceTotal = null });

        Assert.Equal(30m, charge.ServiceTotal);
    }

    [Fact]
    public async Task Declined_charge_is_recorded_as_failed_and_audited()
    {
        var card = TestData.AddCard(_db, _client, "0002");

        var charge = await TestData.Payments(_db).ChargeAsync(Request(card));

        Assert.Equal(PaymentTransactionStatus.Failed, charge.Status);
        Assert.Equal("card_declined", charge.FailureCode);
        Assert.Equal(["payment_failed"], AuditActions());
    }

    [Fact]
    public async Task Refund_marks_the_charge_once_and_ignores_failed_ones()
    {
        var payments = TestData.Payments(_db);
        var approved = await payments.ChargeAsync(Request(TestData.AddCard(_db, _client)));
        var declined = await payments.ChargeAsync(Request(TestData.AddCard(_db, _client, "0002", isDefault: false)));

        Assert.True(await payments.RefundAsync(approved, "client_cancelled", _client.Id));
        Assert.False(await payments.RefundAsync(approved, "again", _client.Id));
        Assert.False(await payments.RefundAsync(declined, "nope", _client.Id));

        using var db = _database.CreateContext();
        var saved = db.PaymentTransactions.AsNoTracking().Single(t => t.Id == approved.Id);
        Assert.Equal(PaymentTransactionStatus.Refunded, saved.Status);
        Assert.StartsWith("sim_re_", saved.RefundReference);
        Assert.Equal("client_cancelled", saved.RefundReason);
        Assert.Equal(PaymentTransactionStatus.Failed, db.PaymentTransactions.AsNoTracking().Single(t => t.Id == declined.Id).Status);
        Assert.Equal(["payment_succeeded", "payment_failed", "payment_refunded"], AuditActions());
    }

    [Fact]
    public async Task Legacy_charges_are_refunded_without_the_gateway()
    {
        var legacy = TestData.AddCharge(_db, TestData.AddBusiness(_db), 20m, DateTime.UtcNow);
        legacy.Gateway = PaymentTransactionsSchema.LegacyGateway;
        _db.SaveChanges();

        Assert.True(await TestData.Payments(_db).RefundAsync(legacy, "admin_refund", null));

        Assert.Equal($"legacy_refund_{legacy.Id}", legacy.RefundReference);
    }

    [Fact]
    public async Task Card_lookup_prefers_the_chosen_card_then_the_default_and_ignores_other_users()
    {
        var first = TestData.AddCard(_db, _client, "1111", isDefault: false);
        var preferred = TestData.AddCard(_db, _client, "2222", isDefault: true);
        var strangers = TestData.AddCard(_db, TestData.AddUser(_db), "3333");
        var payments = TestData.Payments(_db);

        Assert.Equal(first.Id, (await payments.FindCardAsync(_client.Id, first.Id))!.Id);
        Assert.Equal(preferred.Id, (await payments.FindCardAsync(_client.Id, null))!.Id);
        Assert.Equal(preferred.Id, (await payments.FindCardAsync(_client.Id, strangers.Id))!.Id);
        Assert.Null(await payments.FindCardAsync(TestData.AddUser(_db).Id, null));
    }
}
