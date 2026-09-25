using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Accounts.UpdateProfile;
using WebAppPet.Data;
using WebAppPet.Infrastructure.Security;
using WebAppPet.Models;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Accounts;

public class UpdateProfileHandlerTests : IDisposable
{
    private const string OldPassword = "Vieja#2026";
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly AppUser _user;

    public UpdateProfileHandlerTests()
    {
        _db = _database.CreateContext();
        _user = TestData.AddUser(_db);
        _user.FullName = "Ana";
        _user.Email = "ana@test.local";
        _user.City = "Bogotá";
        _user.Latitude = 4.711;
        _user.Longitude = -74.0721;
        _user.CountryCode = "CO";
        _user.PasswordHash = PasswordHasher.Hash(OldPassword);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private UpdateProfileCommand Command(
        string? fullName = "Ana María",
        string? email = "ana@test.local",
        string? phone = null,
        string? city = "Bogotá",
        string? latitude = null,
        string? longitude = null,
        string? newPassword = null,
        string? confirmPassword = null) =>
        new(_user.Id, fullName, email, phone, city, latitude, longitude, newPassword, confirmPassword);

    private Task<UpdateProfileResult> Update(UpdateProfileCommand command) =>
        new UpdateProfileHandler(_database.CreateContext()).HandleAsync(command);

    private AppUser Reload()
    {
        using var db = _database.CreateContext();
        return db.Users.AsNoTracking().Single(u => u.Id == _user.Id);
    }

    [Fact]
    public async Task Keeps_stored_coordinates_when_city_is_unchanged()
    {
        var result = await Update(Command(city: " bogotá ", phone: "(300) 123-4567"));

        Assert.True(result.Success);
        var user = Reload();
        Assert.Equal("Ana María", user.FullName);
        Assert.Equal("3001234567", user.Phone);
        Assert.Equal(4.711, user.Latitude);
        Assert.Equal(4.711, result.Latitude);
        Assert.True(PasswordHasher.Verify(OldPassword, user.PasswordHash));
    }

    [Fact]
    public async Task New_city_with_coordinates_updates_location_and_market()
    {
        var result = await Update(Command(city: "New York", latitude: "40,7128", longitude: "-74.006"));

        Assert.True(result.Success);
        var user = Reload();
        Assert.Equal("New York", user.City);
        Assert.Equal(40.7128, user.Latitude);
        Assert.Equal("US", user.CountryCode);
        Assert.NotNull(user.LocationUpdatedAt);
    }

    [Fact]
    public async Task Blank_city_is_allowed_and_keeps_stored_coordinates()
    {
        var result = await Update(Command(city: ""));

        Assert.True(result.Success);
        Assert.Null(result.Latitude);
        var user = Reload();
        Assert.Equal("", user.City);
        Assert.Equal(4.711, user.Latitude);
    }

    [Fact]
    public async Task Changes_password_when_both_fields_match_the_policy()
    {
        var result = await Update(Command(newPassword: "Nueva#2026", confirmPassword: "Nueva#2026"));

        Assert.True(result.Success);
        Assert.True(PasswordHasher.Verify("Nueva#2026", Reload().PasswordHash));
    }

    [Fact]
    public async Task Allows_keeping_own_email()
    {
        var result = await Update(Command(email: "ANA@test.local"));

        Assert.True(result.Success);
        Assert.Equal("ana@test.local", Reload().Email);
    }

    [Fact]
    public async Task Rejects_email_used_by_another_account()
    {
        var other = TestData.AddUser(_db);

        var result = await Update(Command(email: other.Email));

        Assert.Equal(UpdateProfileError.EmailTaken, result.Error);
    }

    [Fact]
    public async Task Unknown_user_is_not_found()
    {
        var result = await new UpdateProfileHandler(_database.CreateContext())
            .HandleAsync(Command() with { UserId = 999_999 });

        Assert.Equal(UpdateProfileError.NotFound, result.Error);
    }

    [Theory]
    [InlineData(UpdateProfileError.PhoneInvalid, "Ana María", "ana@test.local", "12$45", "Bogotá", null, null)]
    [InlineData(UpdateProfileError.NameRequired, " ", "ana@test.local", null, "Bogotá", null, null)]
    [InlineData(UpdateProfileError.EmailInvalid, "Ana María", "ana", null, "Bogotá", null, null)]
    [InlineData(UpdateProfileError.CityLocationRequired, "Ana María", "ana@test.local", null, "Medellín", null, null)]
    [InlineData(UpdateProfileError.PasswordWeak, "Ana María", "ana@test.local", null, "Bogotá", "corta", "corta")]
    [InlineData(UpdateProfileError.PasswordWeak, "Ana María", "ana@test.local", null, "Bogotá", null, "Nueva#2026")]
    [InlineData(UpdateProfileError.PasswordMismatch, "Ana María", "ana@test.local", null, "Bogotá", "Nueva#2026", "Nueva#2027")]
    public async Task Invalid_input_saves_nothing(
        UpdateProfileError expected, string fullName, string email, string? phone, string city,
        string? newPassword, string? confirmPassword)
    {
        var result = await Update(Command(fullName, email, phone, city, newPassword: newPassword, confirmPassword: confirmPassword));

        Assert.Equal(expected, result.Error);
        var user = Reload();
        Assert.Equal("Ana", user.FullName);
        Assert.Equal("Bogotá", user.City);
        Assert.True(PasswordHasher.Verify(OldPassword, user.PasswordHash));
    }
}
