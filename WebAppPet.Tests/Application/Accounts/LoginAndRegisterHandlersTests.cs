using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Accounts.Login;
using WebAppPet.Application.Accounts.Register;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Infrastructure.Security;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Accounts;

public class LoginAndRegisterHandlersTests : IDisposable
{
    private const string ValidPassword = "Clave#2026";
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;

    public LoginAndRegisterHandlersTests() => _db = _database.CreateContext();

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private AppUser AddUserWithPassword(string email, string password)
    {
        var user = TestData.AddUser(_db);
        user.Email = email;
        user.PasswordHash = PasswordHasher.Hash(password);
        _db.SaveChanges();
        return user;
    }

    private static RegisterCommand ValidRegistration(
        string? fullName = " Ana Pérez ",
        string? email = " Ana@Test.Local ",
        string? phone = "300 123 4567",
        string? city = "Bogotá",
        string? latitude = "4,711",
        string? longitude = "-74.0721",
        string? password = ValidPassword) =>
        new(fullName, email, phone, city, latitude, longitude, password, DefaultCity: "Ubicación guardada");

    private Task<RegisterResult> Register(RegisterCommand command) =>
        new RegisterHandler(_database.CreateContext()).HandleAsync(command);

    [Fact]
    public async Task Login_with_correct_password_returns_user_and_saves_language()
    {
        var user = AddUserWithPassword("ana@test.local", ValidPassword);

        var result = await new LoginHandler(_database.CreateContext())
            .HandleAsync(new LoginCommand("ana@test.local", ValidPassword, "en"));

        Assert.True(result.Success);
        Assert.Equal(user.Id, result.User!.Id);
        using var db = _database.CreateContext();
        Assert.Equal("en", db.Users.AsNoTracking().Single(u => u.Id == user.Id).PreferredLanguage);
    }

    [Theory]
    [InlineData("ana@test.local", "Otra#2026")]
    [InlineData("nadie@test.local", ValidPassword)]
    public async Task Login_fails_on_wrong_password_or_unknown_email(string email, string password)
    {
        AddUserWithPassword("ana@test.local", ValidPassword);

        var result = await new LoginHandler(_database.CreateContext())
            .HandleAsync(new LoginCommand(email, password, "en"));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Signing_in_with_an_old_hash_upgrades_it_to_pbkdf2()
    {
        var user = TestData.AddUser(_db);
        user.Email = "vieja@test.local";
        user.PasswordHash = "71B6822175808E2BCCD340218EF0EB319FC11A62368447891D0744862737F4E6";
        _db.SaveChanges();

        var result = await new LoginHandler(_database.CreateContext())
            .HandleAsync(new LoginCommand("vieja@test.local", "123456", "es"));

        Assert.True(result.Success);
        using var db = _database.CreateContext();
        var stored = db.Users.AsNoTracking().Single(u => u.Id == user.Id).PasswordHash;
        Assert.StartsWith("v2$", stored);
        Assert.Equal(PasswordCheck.Success, PasswordHasher.Check("123456", stored));
    }

    [Fact]
    public async Task A_wrong_password_leaves_the_old_hash_untouched()
    {
        const string legacy = "71B6822175808E2BCCD340218EF0EB319FC11A62368447891D0744862737F4E6";
        var user = TestData.AddUser(_db);
        user.Email = "vieja@test.local";
        user.PasswordHash = legacy;
        _db.SaveChanges();

        var result = await new LoginHandler(_database.CreateContext())
            .HandleAsync(new LoginCommand("vieja@test.local", "654321", "es"));

        Assert.False(result.Success);
        using var db = _database.CreateContext();
        Assert.Equal(legacy, db.Users.AsNoTracking().Single(u => u.Id == user.Id).PasswordHash);
    }

    [Fact]
    public async Task Register_creates_normalized_client_with_welcome_notification()
    {
        var result = await Register(ValidRegistration());

        Assert.True(result.Success);
        using var db = _database.CreateContext();
        var user = db.Users.AsNoTracking().Single(u => u.Id == result.User!.Id);
        Assert.Equal("Ana Pérez", user.FullName);
        Assert.Equal("ana@test.local", user.Email);
        Assert.Equal("3001234567", user.Phone);
        Assert.Equal("Bogotá", user.City);
        Assert.Equal(4.711, user.Latitude);
        Assert.Equal(-74.0721, user.Longitude);
        Assert.Equal("CO", user.CountryCode);
        Assert.Equal(UserRole.Client, user.Role);
        Assert.True(PasswordHasher.Verify(ValidPassword, user.PasswordHash));
        Assert.Equal("promo", db.Notifications.AsNoTracking().Single(n => n.UserId == user.Id).Type);
    }

    [Fact]
    public async Task Register_uses_default_city_when_only_coordinates_are_given()
    {
        var result = await Register(ValidRegistration(city: "  "));

        Assert.True(result.Success);
        Assert.Equal("Ubicación guardada", result.User!.City);
    }

    public static TheoryData<RegisterCommand, RegisterError> InvalidRegistrations => new()
    {
        { ValidRegistration(fullName: " "), RegisterError.NameRequired },
        { ValidRegistration(email: "no-es-correo"), RegisterError.EmailInvalid },
        { ValidRegistration(phone: ""), RegisterError.PhoneRequired },
        { ValidRegistration(phone: "300-ABC"), RegisterError.PhoneInvalid },
        { ValidRegistration(password: "123456789"), RegisterError.PasswordWeak },
        { ValidRegistration(password: "clave#2026"), RegisterError.PasswordWeak },
        { ValidRegistration(latitude: null), RegisterError.LocationRequired },
        { ValidRegistration(latitude: "0", longitude: "0"), RegisterError.LocationRequired },
        { ValidRegistration(latitude: "95"), RegisterError.LocationRequired }
    };

    [Theory]
    [MemberData(nameof(InvalidRegistrations))]
    public async Task Register_rejects_invalid_data_without_saving(RegisterCommand command, RegisterError expected)
    {
        var result = await Register(command);

        Assert.Equal(expected, result.Error);
        using var db = _database.CreateContext();
        Assert.Empty(db.Users.AsNoTracking());
    }

    [Fact]
    public async Task Register_rejects_email_already_in_use_regardless_of_case()
    {
        AddUserWithPassword("ana@test.local", ValidPassword);

        var result = await Register(ValidRegistration(email: "ANA@test.local"));

        Assert.Equal(RegisterError.EmailTaken, result.Error);
        using var db = _database.CreateContext();
        Assert.Single(db.Users.AsNoTracking());
    }
}
