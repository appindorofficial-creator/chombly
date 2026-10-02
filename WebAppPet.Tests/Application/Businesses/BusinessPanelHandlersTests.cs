using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Businesses.MarkVerificationItem;
using WebAppPet.Application.Businesses.RepairCoordinates;
using WebAppPet.Application.Businesses.ResubmitBusiness;
using WebAppPet.Application.Businesses.ScheduleSupportCall;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Businesses;

public class BusinessPanelHandlersTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly GroomerProfile _business;
    private readonly FakeEmailService _email = new();

    public BusinessPanelHandlersTests()
    {
        _db = _database.CreateContext();
        _business = TestData.AddBusiness(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private GroomerProfile Reload()
    {
        using var db = _database.CreateContext();
        return db.Groomers.AsNoTracking().Single(g => g.Id == _business.Id);
    }

    private static T WithCulture<T>(string culture, Func<T> action)
    {
        var (previous, previousUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try { return action(); }
        finally { (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (previous, previousUi); }
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("license")]
    [InlineData("insurance")]
    [InlineData("bank")]
    public async Task Marking_a_verification_step_saves_it(string item)
    {
        var marked = await new MarkVerificationItemHandler(_db).HandleAsync(new MarkVerificationItemCommand(_business.Id, item));

        Assert.True(marked);
        var saved = Reload();
        var flags = new Dictionary<string, bool>
        {
            ["identity"] = saved.VerifiedIdentity,
            ["license"] = saved.VerifiedLicense,
            ["insurance"] = saved.VerifiedInsurance,
            ["bank"] = saved.VerifiedBank
        };
        Assert.True(flags[item]);
        Assert.Single(flags, f => f.Value);
    }

    [Fact]
    public async Task An_unknown_verification_step_changes_nothing()
    {
        var marked = await new MarkVerificationItemHandler(_db).HandleAsync(new MarkVerificationItemCommand(_business.Id, "passport"));

        Assert.False(marked);
        var saved = Reload();
        Assert.False(saved.VerifiedIdentity || saved.VerifiedLicense || saved.VerifiedInsurance || saved.VerifiedBank);
    }

    [Fact]
    public async Task Scheduling_the_support_call_saves_it_and_emails_the_owner_and_the_team()
    {
        var result = await WithCulture("en-US", () =>
            new ScheduleSupportCallHandler(_db, _email).HandleAsync(new ScheduleSupportCallCommand(_business.Id, "2026-10-10", "2:00 PM")));

        Assert.Equal(ScheduleSupportCallError.None, result.Error);
        Assert.Equal(new DateTime(2026, 10, 10, 14, 0, 0), result.When);
        Assert.Equal(new DateTime(2026, 10, 10, 14, 0, 0), Reload().SupportCallAt);
        var owner = _db.Users.Single(u => u.Id == _business.UserId);
        Assert.Equal(new[] { owner.Email, ScheduleSupportCallHandler.TeamEmail }, _email.Sent.Select(m => m.To));
        Assert.Contains(_business.BusinessName, _email.Sent[1].Subject);
    }

    [Theory]
    [InlineData(null, "10:00 AM", ScheduleSupportCallError.InvalidDay)]
    [InlineData("mañana", "10:00 AM", ScheduleSupportCallError.InvalidDay)]
    [InlineData("2026-10-10", "las diez", ScheduleSupportCallError.InvalidSlot)]
    public async Task A_bad_day_or_time_is_rejected_without_saving_or_emailing(string? day, string slot, ScheduleSupportCallError expected)
    {
        var result = await WithCulture("en-US", () =>
            new ScheduleSupportCallHandler(_db, _email).HandleAsync(new ScheduleSupportCallCommand(_business.Id, day, slot)));

        Assert.Equal(expected, result.Error);
        Assert.Null(Reload().SupportCallAt);
        Assert.Empty(_email.Sent);
    }

    [Theory]
    [InlineData(BusinessPublishStatus.Rejected)]
    [InlineData(BusinessPublishStatus.Draft)]
    public async Task A_rejected_or_draft_business_goes_back_to_review_and_admins_are_notified(BusinessPublishStatus status)
    {
        _business.PublishStatus = status;
        _business.IsActive = true;
        _business.IsVerified = true;
        _db.SaveChanges();
        var admin1 = TestData.AddUser(_db, UserRole.Admin);
        var admin2 = TestData.AddUser(_db, UserRole.Admin);

        var resubmitted = await new ResubmitBusinessHandler(_db).HandleAsync(new ResubmitBusinessCommand(_business.Id));

        Assert.True(resubmitted);
        var saved = Reload();
        Assert.Equal(BusinessPublishStatus.PendingReview, saved.PublishStatus);
        Assert.False(saved.IsActive);
        Assert.False(saved.IsVerified);
        using var db = _database.CreateContext();
        var notified = db.Notifications.Where(n => n.Title == "Negocio reenviado a revisión").Select(n => n.UserId).OrderBy(id => id);
        Assert.Equal(new[] { admin1.Id, admin2.Id }, notified);
    }

    [Theory]
    [InlineData(BusinessPublishStatus.Approved)]
    [InlineData(BusinessPublishStatus.PendingReview)]
    public async Task A_published_or_pending_business_cannot_be_resubmitted(BusinessPublishStatus status)
    {
        _business.PublishStatus = status;
        _db.SaveChanges();
        TestData.AddUser(_db, UserRole.Admin);

        var resubmitted = await new ResubmitBusinessHandler(_db).HandleAsync(new ResubmitBusinessCommand(_business.Id));

        Assert.False(resubmitted);
        Assert.Equal(status, Reload().PublishStatus);
        using var db = _database.CreateContext();
        Assert.Empty(db.Notifications);
    }

    [Fact]
    public async Task Coordinates_with_a_lost_decimal_point_are_repaired()
    {
        _business.Latitude = 4711000;
        _business.Longitude = -74072000;
        _db.SaveChanges();

        var repaired = await new RepairCoordinatesHandler(_db).HandleAsync(new RepairCoordinatesCommand(_business.Id));

        Assert.True(repaired);
        var saved = Reload();
        Assert.Equal(4.711, saved.Latitude, 6);
        Assert.Equal(-74.072, saved.Longitude, 6);
    }

    [Fact]
    public async Task Valid_coordinates_are_left_alone()
    {
        _business.Latitude = 4.711;
        _business.Longitude = -74.072;
        _db.SaveChanges();

        var repaired = await new RepairCoordinatesHandler(_db).HandleAsync(new RepairCoordinatesCommand(_business.Id));

        Assert.False(repaired);
        Assert.Equal(4.711, Reload().Latitude, 6);
    }
}
