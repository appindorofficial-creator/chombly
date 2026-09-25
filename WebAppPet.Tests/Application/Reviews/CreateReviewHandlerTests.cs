using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Reviews.CanReview;
using WebAppPet.Application.Reviews.CreateReview;
using WebAppPet.Application.Reviews.Shared;
using WebAppPet.Models;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Reviews;

public class CreateReviewHandlerTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private CreateReviewHandler CreateHandler()
    {
        var db = _database.CreateContext();
        return new CreateReviewHandler(db, new CanReviewHandler(db), new RatingCalculator(db));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Rating_outside_1_to_5_is_rejected(int rating)
    {
        var result = await CreateHandler().HandleAsync(new CreateReviewCommand(1, 1, rating, "ok"));

        Assert.False(result.Success);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public async Task Client_without_eligible_visit_cannot_review()
    {
        using var db = _database.CreateContext();
        var client = TestData.AddUser(db);
        var business = TestData.AddBusiness(db);
        TestData.AddAppointment(db, client, business, AppointmentStatus.Pending, DateTime.UtcNow.AddDays(-1));

        var result = await CreateHandler().HandleAsync(new CreateReviewCommand(client.Id, business.Id, 5, "Great"));

        Assert.False(result.Success);
        using var check = _database.CreateContext();
        Assert.Equal(0, await check.Reviews.CountAsync());
    }

    [Fact]
    public async Task Valid_review_is_saved_trimmed_and_updates_business_rating()
    {
        using var db = _database.CreateContext();
        var business = TestData.AddBusiness(db);
        var previous = TestData.AddUser(db);
        TestData.AddReview(db, previous, business, 4);
        var client = TestData.AddUser(db);
        TestData.AddAppointment(db, client, business, AppointmentStatus.Completed, DateTime.UtcNow.AddDays(-1));
        var longComment = "  " + new string('a', 700) + "  ";

        var result = await CreateHandler().HandleAsync(new CreateReviewCommand(client.Id, business.Id, 5, longComment));

        Assert.True(result.Success);
        using var check = _database.CreateContext();
        var saved = await check.Reviews.SingleAsync(r => r.ClientId == client.Id);
        Assert.Equal(5, saved.Rating);
        Assert.Equal(CreateReviewHandler.MaxCommentLength, saved.Comment.Length);
        var updated = await check.Groomers.SingleAsync(g => g.Id == business.Id);
        Assert.Equal(2, updated.ReviewCount);
        Assert.Equal(4.5, updated.Rating);
    }

    [Fact]
    public async Task Reviewing_a_past_confirmed_appointment_marks_it_completed()
    {
        using var db = _database.CreateContext();
        var client = TestData.AddUser(db);
        var business = TestData.AddBusiness(db);
        var appt = TestData.AddAppointment(db, client, business, AppointmentStatus.Confirmed, DateTime.UtcNow.AddHours(-3));

        var result = await CreateHandler().HandleAsync(
            new CreateReviewCommand(client.Id, business.Id, 4, "Bien", appt.Id));

        Assert.True(result.Success);
        using var check = _database.CreateContext();
        Assert.Equal(AppointmentStatus.Completed, (await check.Appointments.SingleAsync(a => a.Id == appt.Id)).Status);
    }

    [Fact]
    public async Task Second_review_for_the_same_business_is_rejected()
    {
        using var db = _database.CreateContext();
        var client = TestData.AddUser(db);
        var business = TestData.AddBusiness(db);
        TestData.AddAppointment(db, client, business, AppointmentStatus.Completed, DateTime.UtcNow.AddDays(-1));

        var first = await CreateHandler().HandleAsync(new CreateReviewCommand(client.Id, business.Id, 5, "1"));
        var second = await CreateHandler().HandleAsync(new CreateReviewCommand(client.Id, business.Id, 3, "2"));

        Assert.True(first.Success);
        Assert.False(second.Success);
        using var check = _database.CreateContext();
        Assert.Equal(1, await check.Reviews.CountAsync(r => r.ClientId == client.Id));
    }
}
