using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebAppPet.Application.Businesses.CreateBusiness;
using WebAppPet.Application.Businesses.Shared;
using WebAppPet.Data;
using WebAppPet.Infrastructure.Security;
using WebAppPet.Models;
using WebAppPet.Services;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Businesses;

public class CreateBusinessHandlerTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly FakeEmailService _email = new();
    private readonly ServiceCategory _walkers;
    private readonly ServiceCategory _hotel;
    private readonly AppUser _admin;

    public CreateBusinessHandlerTests()
    {
        _db = _database.CreateContext();
        _walkers = AddCategory("walkers", overnight: false);
        _hotel = AddCategory("hotel", overnight: true);
        _admin = TestData.AddUser(_db, UserRole.Admin);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private ServiceCategory AddCategory(string slug, bool overnight)
    {
        var category = new ServiceCategory { Slug = slug, Name = slug, IsOvernight = overnight };
        _db.Categories.Add(category);
        _db.SaveChanges();
        return category;
    }

    private CreateBusinessCommand Command(params ServiceCategory[] categories) => new()
    {
        ProviderKindKey = "independent",
        FullName = " Ana ",
        BusinessName = " Paseos Ana ",
        Phone = "3001234567",
        Email = " Ana@Test.Local ",
        City = "Bogotá",
        Latitude = 4.711,
        Longitude = -74.0721,
        PrimaryCategoryId = categories[0].Id,
        CategoryIds = categories.Select(c => c.Id).ToList(),
        WorkModeKey = "domicilio",
        Week = WeekDayInput.DefaultWeek(),
        ServiceArea = 10,
        About = "Paseos por el parque todos los días",
        ServiceNames = ["Paseo 30 min", " ", "Paseo 60 min"],
        ServicePrices = [15000, 99, 25000],
        Password = "Clave#2026",
        HotelExtras = new HotelExtraPrices(BathPrice: 20000, MedsPrice: 5000, OffersPrivateCamera: true, PrivateCameraPrice: 8000)
    };

    private Task<CreateBusinessResult> Create(CreateBusinessCommand command)
    {
        var db = _database.CreateContext();
        return new CreateBusinessHandler(
                db,
                new AvailabilityService(db),
                _email,
                Options.Create(new SmtpOptions { From = "no-reply@chombly.app", AdminNotifyEmail = "admin@chombly.app" }))
            .HandleAsync(command);
    }

    private GroomerProfile LoadBusiness(int id)
    {
        using var db = _database.CreateContext();
        return db.Groomers.AsNoTracking()
            .Include(g => g.User)
            .Include(g => g.Services)
            .Single(g => g.Id == id);
    }

    [Fact]
    public async Task Guest_gets_a_new_groomer_account_and_a_business_pending_review()
    {
        var result = await Create(Command(_walkers));

        Assert.True(result.Success);
        var business = LoadBusiness(result.BusinessId!.Value);
        Assert.Equal("ana@test.local", business.User.Email);
        Assert.Equal("Ana", business.User.FullName);
        Assert.Equal(UserRole.Groomer, business.User.Role);
        Assert.True(PasswordHasher.Verify("Clave#2026", business.User.PasswordHash));

        Assert.Equal("Paseos Ana", business.BusinessName);
        Assert.Equal(BusinessPublishStatus.PendingReview, business.PublishStatus);
        Assert.False(business.IsActive);
        Assert.Equal(ProviderKind.Independent, business.ProviderKind);
        Assert.Equal(WorkMode.Mobile, business.WorkMode);
        Assert.Equal(GroomerType.Mobile, business.Type);
        Assert.Equal("CO", business.LicenseCountry);
        Assert.Equal(6, business.ServiceAreaMiles);
        Assert.Equal("Bogotá", business.Address);
        Assert.Equal(15000, business.StartingPrice);
        Assert.Equal("/images/categories/cat-walkers-v2.png", business.ImageUrl);

        Assert.Equal(["Paseo 30 min", "Paseo 60 min"], business.Services.OrderBy(s => s.Id).Select(s => s.Name));
        Assert.Equal([30, 60], business.Services.OrderBy(s => s.Id).Select(s => s.DurationMinutes));
    }

    [Fact]
    public async Task Creates_schedule_notifications_and_both_emails()
    {
        var result = await Create(Command(_walkers));

        using var db = _database.CreateContext();
        var businessId = result.BusinessId!.Value;
        Assert.Equal(7, db.WeeklyHours.Count(h => h.GroomerId == businessId));
        Assert.Equal(CreateBusinessHandler.AgendaDays, db.DayAvailabilities.Count(d => d.GroomerId == businessId));
        Assert.Equal("¡Bienvenido a Chombly!", db.Notifications.Single(n => n.UserId == result.User!.Id).Title);
        Assert.Equal("Nuevo negocio pendiente", db.Notifications.Single(n => n.UserId == _admin.Id).Title);
        Assert.Equal(["ana@test.local", "admin@chombly.app"], _email.Sent.Select(m => m.To));
    }

    [Fact]
    public async Task Hotel_gets_overnight_services_and_its_extras()
    {
        var result = await Create(Command(_hotel, _walkers) with { ServiceNames = ["Noche estándar"], ServicePrices = [90000] });

        var business = LoadBusiness(result.BusinessId!.Value);
        Assert.Equal(1440, business.Services.Single().DurationMinutes);
        Assert.Equal(_walkers.Id.ToString(), business.ExtraCategoryIds);
        using var db = _database.CreateContext();
        var extras = db.ServiceExtras.AsNoTracking().Where(e => e.GroomerId == business.Id).ToList();
        Assert.Equal(20000, extras.Single(e => HotelCoreExtras.MatchesBath(e.Name)).Price);
        Assert.Equal(8000, extras.Single(e => HotelPrivateCameraExtra.Matches(e.Name)).Price);
    }

    [Fact]
    public async Task Signed_in_client_is_converted_to_groomer()
    {
        var client = TestData.AddUser(_db);
        client.PasswordHash = PasswordHasher.Hash("Vieja#2026");
        _db.SaveChanges();

        var result = await Create(Command(_walkers) with { CurrentUserId = client.Id, Password = null });

        Assert.True(result.Success);
        Assert.Equal(client.Id, result.User!.Id);
        var business = LoadBusiness(result.BusinessId!.Value);
        Assert.Equal(UserRole.Groomer, business.User.Role);
        Assert.Equal("ana@test.local", business.User.Email);
        Assert.True(PasswordHasher.Verify("Vieja#2026", business.User.PasswordHash));
    }

    [Fact]
    public async Task User_that_already_has_a_business_is_rejected()
    {
        var owner = TestData.AddBusiness(_db);

        var result = await Create(Command(_walkers) with { CurrentUserId = owner.UserId });

        Assert.Equal(RegistrationError.AlreadyBusiness, result.Error);
    }

    [Fact]
    public async Task Missing_session_user_is_rejected()
    {
        var result = await Create(Command(_walkers) with { CurrentUserId = 999_999 });

        Assert.Equal(RegistrationError.InvalidSession, result.Error);
    }

    [Theory]
    [InlineData(true, RegistrationError.EmailTaken)]
    [InlineData(false, RegistrationError.EmailTakenLogin)]
    public async Task Email_used_by_another_account_is_rejected(bool signedIn, RegistrationError expected)
    {
        var other = TestData.AddUser(_db);
        other.Email = "ana@test.local";
        var client = TestData.AddUser(_db);
        _db.SaveChanges();

        var result = await Create(Command(_walkers) with { CurrentUserId = signedIn ? client.Id : null });

        Assert.Equal(expected, result.Error);
        using var db = _database.CreateContext();
        Assert.Empty(db.Groomers.AsNoTracking());
    }

    [Fact]
    public async Task Unknown_category_saves_nothing()
    {
        var usersBefore = _db.Users.Count();

        var result = await Create(Command(_walkers) with { PrimaryCategoryId = 999_999 });

        Assert.Equal(RegistrationError.InvalidCategory, result.Error);
        using var db = _database.CreateContext();
        Assert.Equal(usersBefore, db.Users.Count());
        Assert.Empty(_email.Sent);
    }
}
