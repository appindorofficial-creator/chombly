using WebAppPet.Application.Favorites.GetFavoriteIds;
using WebAppPet.Application.Favorites.GetFavorites;
using WebAppPet.Application.Favorites.ToggleFavorite;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Favorites;

public class FavoritesTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly AppUser _user;

    public FavoritesTests()
    {
        _db = _database.CreateContext();
        _user = TestData.AddUser(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private GroomerProfile AddVisibleBusiness()
    {
        var business = TestData.AddBusiness(_db);
        business.IsActive = true;
        business.PublishStatus = BusinessPublishStatus.Approved;
        _db.SaveChanges();
        return business;
    }

    private Task<ToggleFavoriteOutcome> Toggle(int groomerId, AppUser? user = null) =>
        new ToggleFavoriteHandler(_db).HandleAsync(new ToggleFavoriteCommand((user ?? _user).Id, groomerId));

    private Task<HashSet<int>> FavoriteIds(AppUser? user = null) =>
        new GetFavoriteIdsHandler(_db).HandleAsync(new GetFavoriteIdsQuery((user ?? _user).Id));

    [Fact]
    public async Task Toggling_adds_the_business_and_toggling_again_removes_it()
    {
        var business = AddVisibleBusiness();

        Assert.Equal(ToggleFavoriteOutcome.Added, await Toggle(business.Id));
        Assert.Equal([business.Id], await FavoriteIds());

        Assert.Equal(ToggleFavoriteOutcome.Removed, await Toggle(business.Id));
        Assert.Empty(await FavoriteIds());
    }

    [Fact]
    public async Task A_missing_business_cannot_be_favorited()
    {
        Assert.Equal(ToggleFavoriteOutcome.NotFound, await Toggle(999));

        using var verify = _database.CreateContext();
        Assert.Empty(verify.Favorites);
    }

    [Fact]
    public async Task An_inactive_business_cannot_be_favorited()
    {
        var business = AddVisibleBusiness();
        business.IsActive = false;
        _db.SaveChanges();

        Assert.Equal(ToggleFavoriteOutcome.NotFound, await Toggle(business.Id));
    }

    [Fact]
    public async Task A_business_pending_approval_cannot_be_favorited()
    {
        var business = AddVisibleBusiness();
        business.PublishStatus = BusinessPublishStatus.PendingReview;
        _db.SaveChanges();

        Assert.Equal(ToggleFavoriteOutcome.NotFound, await Toggle(business.Id));
    }

    [Fact]
    public async Task A_favorite_can_still_be_removed_after_the_business_is_deactivated()
    {
        var business = AddVisibleBusiness();
        await Toggle(business.Id);
        business.IsActive = false;
        _db.SaveChanges();

        Assert.Equal(ToggleFavoriteOutcome.Removed, await Toggle(business.Id));
        Assert.Empty(await FavoriteIds());
    }

    [Fact]
    public async Task The_favorites_page_hides_unpublished_businesses_and_shows_the_newest_first()
    {
        var first = AddVisibleBusiness();
        var deactivated = AddVisibleBusiness();
        var latest = AddVisibleBusiness();
        await Toggle(first.Id);
        await Toggle(deactivated.Id);
        await Toggle(latest.Id);
        deactivated.IsActive = false;
        _db.SaveChanges();

        var favorites = await new GetFavoritesHandler(_db).HandleAsync(new GetFavoritesQuery(_user.Id));

        Assert.Equal([latest.Id, first.Id], favorites.Select(g => g.Id));
    }

    [Fact]
    public async Task Favorites_belong_to_each_user()
    {
        var business = AddVisibleBusiness();
        var other = TestData.AddUser(_db);
        await Toggle(business.Id, other);

        Assert.Empty(await FavoriteIds());
        Assert.Empty(await new GetFavoritesHandler(_db).HandleAsync(new GetFavoritesQuery(_user.Id)));
    }

    [Fact]
    public async Task The_favorites_page_lists_the_saved_businesses_with_their_category()
    {
        var category = new ServiceCategory { Name = "Peluquería", Slug = "fav-grooming" };
        _db.Categories.Add(category);
        var business = AddVisibleBusiness();
        business.Category = category;
        _db.SaveChanges();
        await Toggle(business.Id);

        var favorites = await new GetFavoritesHandler(_db).HandleAsync(new GetFavoritesQuery(_user.Id));

        var listed = Assert.Single(favorites);
        Assert.Equal(business.Id, listed.Id);
        Assert.Equal("fav-grooming", listed.Category?.Slug);
    }
}
