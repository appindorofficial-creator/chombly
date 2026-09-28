using WebAppPet.Application.Accounts.SaveLocation;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Accounts;

public class SaveLocationTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly AppUser _user;

    public SaveLocationTests()
    {
        _db = _database.CreateContext();
        _user = TestData.AddUser(_db);
        _user.City = "Neiva";
        _user.CountryCode = "CO";
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private Task<SaveLocationResult> Save(string? lat, string? lng, string? city = null, int? userId = null) =>
        new SaveLocationHandler(_db).HandleAsync(new SaveLocationCommand(userId ?? _user.Id, lat, lng, city));

    private AppUser Reload()
    {
        using var verify = _database.CreateContext();
        return verify.Users.Single(u => u.Id == _user.Id);
    }

    [Fact]
    public async Task Saves_the_browser_position_with_a_comma_or_dot_decimal()
    {
        var result = await Save("2,9273", "-75.2819");

        Assert.Equal(SaveLocationOutcome.Saved, result.Outcome);
        var user = Reload();
        Assert.Equal(2.9273, user.Latitude);
        Assert.Equal(-75.2819, user.Longitude);
        Assert.NotNull(user.LocationUpdatedAt);
        Assert.Equal("Neiva", result.City);
    }

    [Fact]
    public async Task A_new_city_is_trimmed_and_capped_and_a_blank_one_keeps_the_old()
    {
        await Save("4.6", "-74.08", "  Bogotá  ");
        Assert.Equal("Bogotá", Reload().City);

        await Save("4.6", "-74.08", "   ");
        Assert.Equal("Bogotá", Reload().City);

        await Save("4.6", "-74.08", new string('x', 200));
        Assert.Equal(120, Reload().City!.Length);
    }

    [Fact]
    public async Task A_position_ping_does_not_change_the_home_country()
    {
        await Save("35.2271", "-80.8431", "Charlotte");

        Assert.Equal("CO", Reload().CountryCode);
    }

    [Theory]
    [InlineData(null, "-74.08")]
    [InlineData("abc", "-74.08")]
    [InlineData("91", "-74.08")]
    [InlineData("4.6", "-181")]
    public async Task Rejects_missing_or_out_of_range_coordinates(string? lat, string? lng)
    {
        var result = await Save(lat, lng);

        Assert.Equal(SaveLocationOutcome.InvalidCoordinates, result.Outcome);
        Assert.Null(Reload().Latitude);
    }

    [Fact]
    public async Task An_unknown_user_is_reported()
    {
        var result = await Save("4.6", "-74.08", userId: 999);

        Assert.Equal(SaveLocationOutcome.UserNotFound, result.Outcome);
    }
}
