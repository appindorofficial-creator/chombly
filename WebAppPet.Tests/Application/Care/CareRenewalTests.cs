using WebAppPet.Application.Care.RenewCare;
using WebAppPet.Application.Care.Shared;
using WebAppPet.Application.Common;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Care;

public class CareRenewalTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly AppUser _user;

    public CareRenewalTests()
    {
        _db = _database.CreateContext();
        _user = TestData.AddUser(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private Task<int> RenewDue() =>
        new RenewDueCareHandler(_db, new ServiceCatalogService(_db), TestData.Payments(_db), new VetAuditService(_db))
            .HandleAsync(new RenewDueCareCommand(Now));

    private CareSubscription AddSubscription(DateTime periodEnd, string currency = "COP", bool cancelAtPeriodEnd = false)
    {
        var subscription = new CareSubscription
        {
            UserId = _user.Id,
            PricePerMonth = 14.99m,
            Currency = currency,
            CurrentPeriodStart = periodEnd.AddMonths(-1),
            CurrentPeriodEnd = periodEnd,
            CancelAtPeriodEnd = cancelAtPeriodEnd
        };
        _db.CareSubscriptions.Add(subscription);
        _db.SaveChanges();
        return subscription;
    }

    [Theory]
    [InlineData("COP", 50_000)]
    [InlineData("USD", 14.99)]
    public async Task An_ended_cycle_is_charged_at_the_current_price_of_its_currency(string currency, double expected)
    {
        TestData.AddCard(_db, _user);
        var ended = Now.AddHours(-2);
        var subscription = AddSubscription(ended, currency);

        Assert.Equal(1, await RenewDue());

        using var verify = _database.CreateContext();
        var charge = verify.PaymentTransactions.Single();
        Assert.Equal((PaymentPurpose.CareSubscription, PaymentTransactionStatus.Succeeded, (decimal)expected, currency, subscription.Id),
            (charge.Purpose, charge.Status, charge.Amount, charge.Currency, charge.CareSubscriptionId));
        var renewed = verify.CareSubscriptions.Single();
        Assert.Equal((CareSubscriptionStatus.Active, ended, ended.AddMonths(1), (decimal)expected),
            (renewed.Status, renewed.CurrentPeriodStart, renewed.CurrentPeriodEnd, renewed.PricePerMonth));
        var notice = verify.Notifications.Single();
        Assert.Equal(("care", _user.Id), (notice.Type, notice.UserId));
        Assert.Contains("4242", notice.Message);
    }

    [Fact]
    public async Task A_membership_left_unrenewed_for_months_starts_its_new_cycle_now()
    {
        TestData.AddCard(_db, _user);
        AddSubscription(Now.AddMonths(-3));

        await RenewDue();

        using var verify = _database.CreateContext();
        var renewed = verify.CareSubscriptions.Single();
        Assert.Equal((Now, Now.AddMonths(1)), (renewed.CurrentPeriodStart, renewed.CurrentPeriodEnd));
        Assert.Single(verify.PaymentTransactions);
    }

    [Fact]
    public async Task A_declined_renewal_pauses_the_membership_and_tells_the_family()
    {
        TestData.AddCard(_db, _user, last4: "0002");
        AddSubscription(Now.AddHours(-2));

        await RenewDue();

        using var verify = _database.CreateContext();
        Assert.Equal(CareSubscriptionStatus.PastDue, verify.CareSubscriptions.Single().Status);
        Assert.Equal(PaymentTransactionStatus.Failed, verify.PaymentTransactions.Single().Status);
        var notice = verify.Notifications.Single();
        Assert.Equal("care", notice.Type);
        Assert.Contains("0002", notice.Message);
        Assert.Null(await new ChomblyCareService(verify).GetActiveAsync(_user.Id));
    }

    [Fact]
    public async Task Without_a_card_the_membership_is_paused_and_nothing_is_charged()
    {
        AddSubscription(Now.AddHours(-2));

        await RenewDue();

        using var verify = _database.CreateContext();
        Assert.Equal(CareSubscriptionStatus.PastDue, verify.CareSubscriptions.Single().Status);
        Assert.Empty(verify.PaymentTransactions);
        Assert.Equal("care", verify.Notifications.Single().Type);
    }

    [Fact]
    public async Task A_membership_set_to_cancel_ends_without_a_charge()
    {
        TestData.AddCard(_db, _user);
        AddSubscription(Now.AddHours(-2), cancelAtPeriodEnd: true);

        await RenewDue();

        using var verify = _database.CreateContext();
        Assert.Equal(CareSubscriptionStatus.Cancelled, verify.CareSubscriptions.Single().Status);
        Assert.Empty(verify.PaymentTransactions);
        Assert.Single(verify.Notifications);
    }

    [Fact]
    public async Task Memberships_within_their_cycle_are_left_alone()
    {
        TestData.AddCard(_db, _user);
        var subscription = AddSubscription(Now.AddDays(3));

        Assert.Equal(0, await RenewDue());

        using var verify = _database.CreateContext();
        Assert.Equal(subscription.CurrentPeriodEnd, verify.CareSubscriptions.Single().CurrentPeriodEnd);
        Assert.Empty(verify.PaymentTransactions);
        Assert.Empty(verify.Notifications);
    }

    [Fact]
    public async Task Reading_the_membership_no_longer_extends_an_ended_cycle_for_free()
    {
        var ended = DateTime.UtcNow.AddHours(-2);
        AddSubscription(ended);

        var active = await new ChomblyCareService(_db).GetActiveAsync(_user.Id);

        Assert.NotNull(active);
        Assert.Equal(ended, active.CurrentPeriodEnd);
    }
}
