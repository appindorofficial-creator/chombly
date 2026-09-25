using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Businesses.ApproveBusiness;
using WebAppPet.Application.Businesses.GetPendingBusinesses;
using WebAppPet.Application.Businesses.RejectBusiness;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Businesses;

public class BusinessApprovalHandlersTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly FakeEmailService _email = new();

    public BusinessApprovalHandlersTests() => _db = _database.CreateContext();

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private GroomerProfile AddPendingBusiness()
    {
        var business = TestData.AddBusiness(_db);
        business.PublishStatus = BusinessPublishStatus.PendingReview;
        business.IsActive = false;
        business.IsVerified = false;
        _db.SaveChanges();
        return business;
    }

    private GroomerProfile Reload(int id)
    {
        using var db = _database.CreateContext();
        return db.Groomers.AsNoTracking().Single(g => g.Id == id);
    }

    private AppNotification SingleNotification(int userId)
    {
        using var db = _database.CreateContext();
        return db.Notifications.AsNoTracking().Single(n => n.UserId == userId);
    }

    [Fact]
    public async Task Approve_publishes_business_and_notifies_owner()
    {
        var business = AddPendingBusiness();

        var name = await new ApproveBusinessHandler(_database.CreateContext(), _email)
            .HandleAsync(new ApproveBusinessCommand(business.Id));

        Assert.Equal(business.BusinessName, name);
        var saved = Reload(business.Id);
        Assert.Equal(BusinessPublishStatus.Approved, saved.PublishStatus);
        Assert.True(saved.IsActive);
        Assert.True(saved.IsVerified);
        Assert.Equal("¡Negocio publicado!", SingleNotification(business.UserId).Title);
        var mail = Assert.Single(_email.Sent);
        Assert.Contains("publicado", mail.Subject);
        Assert.Contains(business.BusinessName, mail.Body);
    }

    [Fact]
    public async Task Reject_hides_business_and_notifies_owner()
    {
        var business = AddPendingBusiness();

        var name = await new RejectBusinessHandler(_database.CreateContext(), _email)
            .HandleAsync(new RejectBusinessCommand(business.Id));

        Assert.Equal(business.BusinessName, name);
        var saved = Reload(business.Id);
        Assert.Equal(BusinessPublishStatus.Rejected, saved.PublishStatus);
        Assert.False(saved.IsActive);
        Assert.False(saved.IsVerified);
        Assert.Equal("Solicitud no aprobada", SingleNotification(business.UserId).Title);
        Assert.Contains("no aprobada", Assert.Single(_email.Sent).Subject);
    }

    [Fact]
    public async Task Unknown_business_returns_null_and_sends_nothing()
    {
        Assert.Null(await new ApproveBusinessHandler(_database.CreateContext(), _email)
            .HandleAsync(new ApproveBusinessCommand(999_999)));
        Assert.Null(await new RejectBusinessHandler(_database.CreateContext(), _email)
            .HandleAsync(new RejectBusinessCommand(999_999)));
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task Pending_list_only_includes_businesses_in_review_newest_first()
    {
        var older = AddPendingBusiness();
        var newer = AddPendingBusiness();
        TestData.AddBusiness(_db);

        var pending = await new GetPendingBusinessesHandler(_database.CreateContext()).HandleAsync();

        Assert.Equal([newer.Id, older.Id], pending.Select(g => g.Id));
        Assert.All(pending, g => Assert.NotNull(g.User));
    }
}
