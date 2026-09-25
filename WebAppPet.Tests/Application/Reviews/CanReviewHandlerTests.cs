using WebAppPet.Application.Reviews.CanReview;
using WebAppPet.Models;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Reviews;

public class CanReviewHandlerTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private Task<CanReviewResult> CheckAsync(int clientId, int groomerId, int? appointmentId = null) =>
        new CanReviewHandler(_database.CreateContext())
            .HandleAsync(new CanReviewQuery(clientId, groomerId, appointmentId));

    [Fact]
    public async Task Client_without_visits_cannot_review()
    {
        using var db = _database.CreateContext();
        var client = TestData.AddUser(db);
        var business = TestData.AddBusiness(db);

        var result = await CheckAsync(client.Id, business.Id);

        Assert.False(result.CanReview);
        Assert.False(result.AlreadyReviewed);
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed, 2, true)]
    [InlineData(AppointmentStatus.Completed, -2, true)]
    [InlineData(AppointmentStatus.Confirmed, -2, true)]
    [InlineData(AppointmentStatus.Confirmed, 2, false)]
    [InlineData(AppointmentStatus.Pending, -2, false)]
    [InlineData(AppointmentStatus.Cancelled, -2, false)]
    public async Task Eligibility_depends_on_status_and_start_time(AppointmentStatus status, int hoursFromNow, bool expected)
    {
        using var db = _database.CreateContext();
        var client = TestData.AddUser(db);
        var business = TestData.AddBusiness(db);
        TestData.AddAppointment(db, client, business, status, DateTime.UtcNow.AddHours(hoursFromNow));

        var result = await CheckAsync(client.Id, business.Id);

        Assert.Equal(expected, result.CanReview);
    }

    [Fact]
    public async Task Already_reviewed_business_is_not_eligible_again()
    {
        using var db = _database.CreateContext();
        var client = TestData.AddUser(db);
        var business = TestData.AddBusiness(db);
        TestData.AddAppointment(db, client, business, AppointmentStatus.Completed, DateTime.UtcNow.AddDays(-1));
        TestData.AddReview(db, client, business, 5);

        var result = await CheckAsync(client.Id, business.Id);

        Assert.False(result.CanReview);
        Assert.True(result.AlreadyReviewed);
    }

    [Fact]
    public async Task Appointment_filter_only_considers_that_appointment()
    {
        using var db = _database.CreateContext();
        var client = TestData.AddUser(db);
        var business = TestData.AddBusiness(db);
        TestData.AddAppointment(db, client, business, AppointmentStatus.Completed, DateTime.UtcNow.AddDays(-3));
        var pending = TestData.AddAppointment(db, client, business, AppointmentStatus.Pending, DateTime.UtcNow.AddDays(1));

        var result = await CheckAsync(client.Id, business.Id, pending.Id);

        Assert.False(result.CanReview);
    }
}
