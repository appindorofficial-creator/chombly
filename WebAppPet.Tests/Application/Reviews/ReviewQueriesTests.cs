using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Reviews.GetReviews;
using WebAppPet.Application.Reviews.Shared;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Reviews;

public class ReviewQueriesTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task GetReviews_returns_newest_first_limited_and_with_client()
    {
        using var db = _database.CreateContext();
        var business = TestData.AddBusiness(db);
        var other = TestData.AddBusiness(db);
        var now = DateTime.UtcNow;
        for (var i = 0; i < 4; i++)
            TestData.AddReview(db, TestData.AddUser(db), business, 3 + (i % 3), now.AddDays(-i));
        TestData.AddReview(db, TestData.AddUser(db), other, 1);

        var reviews = await new GetReviewsHandler(_database.CreateContext())
            .HandleAsync(new GetReviewsQuery(business.Id, Take: 3));

        Assert.Equal(3, reviews.Count);
        Assert.All(reviews, r => Assert.Equal(business.Id, r.GroomerId));
        Assert.All(reviews, r => Assert.NotNull(r.Client));
        Assert.True(reviews.SequenceEqual(reviews.OrderByDescending(r => r.CreatedAt)));
    }

    [Fact]
    public async Task RatingCalculator_averages_reviews_to_one_decimal()
    {
        using var db = _database.CreateContext();
        var business = TestData.AddBusiness(db);
        TestData.AddReview(db, TestData.AddUser(db), business, 5);
        TestData.AddReview(db, TestData.AddUser(db), business, 4);
        TestData.AddReview(db, TestData.AddUser(db), business, 4);

        await new RatingCalculator(_database.CreateContext()).RecalculateAsync(business.Id);

        using var check = _database.CreateContext();
        var updated = await check.Groomers.SingleAsync(g => g.Id == business.Id);
        Assert.Equal(3, updated.ReviewCount);
        Assert.Equal(4.3, updated.Rating);
    }

    [Fact]
    public async Task RatingCalculator_resets_business_without_reviews()
    {
        using var db = _database.CreateContext();
        var business = TestData.AddBusiness(db);
        business.Rating = 4.8;
        business.ReviewCount = 120;
        db.SaveChanges();

        await new RatingCalculator(_database.CreateContext()).RecalculateAsync(business.Id);

        using var check = _database.CreateContext();
        var updated = await check.Groomers.SingleAsync(g => g.Id == business.Id);
        Assert.Equal(0, updated.ReviewCount);
        Assert.Equal(0, updated.Rating);
    }
}
