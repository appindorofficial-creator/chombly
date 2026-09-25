using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Businesses.AddAmenity;
using WebAppPet.Application.Businesses.AddExtra;
using WebAppPet.Application.Businesses.AddService;
using WebAppPet.Application.Businesses.UpdateBusiness;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Businesses;

public class UpdateBusinessHandlerTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly GroomerProfile _business;
    private readonly ServiceCategory _grooming;
    private readonly ServiceCategory _hotel;
    private readonly ServiceCategory _inactive;

    public UpdateBusinessHandlerTests()
    {
        _db = _database.CreateContext();
        _grooming = AddCategory("grooming", sortOrder: 1);
        _hotel = AddCategory("hotel", sortOrder: 2);
        _inactive = AddCategory("old", sortOrder: 0, isActive: false);
        _business = TestData.AddBusiness(_db);
        _business.BusinessName = "Original";
        _business.Phone = "3000000000";
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private ServiceCategory AddCategory(string slug, int sortOrder, bool isActive = true)
    {
        var category = new ServiceCategory { Slug = slug, Name = slug, SortOrder = sortOrder, IsActive = isActive };
        _db.Categories.Add(category);
        _db.SaveChanges();
        return category;
    }

    private UpdateBusinessCommand Command(params int[] categoryIds) => new()
    {
        BusinessId = _business.Id,
        BusinessName = "  Peluquería Luna ",
        CategoryIds = categoryIds,
        Address = " Calle 1 ",
        City = " Bogotá ",
        Latitude = 4.711,
        Longitude = -74.0721,
        About = " Bañamos perros ",
        Phone = "300 123 4567",
        IsActive = true,
        HotelExtras = new HotelExtraPrices(BathPrice: 20000, MedsPrice: 5000, OffersPrivateCamera: true, PrivateCameraPrice: 8000)
    };

    private Task<UpdateBusinessError> Update(UpdateBusinessCommand command) =>
        new UpdateBusinessHandler(_database.CreateContext()).HandleAsync(command);

    private GroomerProfile Reload()
    {
        using var db = _database.CreateContext();
        return db.Groomers.AsNoTracking().Include(g => g.User).Single(g => g.Id == _business.Id);
    }

    private List<ServiceExtra> Extras()
    {
        using var db = _database.CreateContext();
        return db.ServiceExtras.AsNoTracking().Where(e => e.GroomerId == _business.Id).ToList();
    }

    [Fact]
    public async Task Saves_trimmed_profile_and_aligns_market_with_location()
    {
        var error = await Update(Command(_grooming.Id) with { City = "New York", Latitude = 40.7128, Longitude = -74.006 });

        Assert.Equal(UpdateBusinessError.None, error);
        var saved = Reload();
        Assert.Equal("Peluquería Luna", saved.BusinessName);
        Assert.Equal("Calle 1", saved.Address);
        Assert.Equal("Bañamos perros", saved.About);
        Assert.Equal("3001234567", saved.Phone);
        Assert.Equal(_grooming.Id, saved.CategoryId);
        Assert.Equal("US", saved.User.CountryCode);
        Assert.Equal("US", saved.LicenseCountry);
        Assert.Equal(PetSpecies.DefaultAcceptedList, saved.AcceptedSpecies);
    }

    [Fact]
    public async Task Repairs_coordinates_that_lost_their_decimal_comma()
    {
        await Update(Command(_grooming.Id) with { Latitude = 28861202, Longitude = -81234567 });

        var saved = Reload();
        Assert.Equal(28.861202, saved.Latitude, 6);
        Assert.Equal(-81.234567, saved.Longitude, 6);
    }

    [Fact]
    public async Task Keeps_requested_primary_category_when_it_is_selected()
    {
        await Update(Command(_grooming.Id, _hotel.Id) with { CategoryId = _hotel.Id });

        var saved = Reload();
        Assert.Equal(_hotel.Id, saved.CategoryId);
        Assert.Equal([_hotel.Id, _grooming.Id], saved.GetOfferedCategoryIds());
    }

    [Fact]
    public async Task Picks_first_selected_category_by_sort_order_when_primary_is_not_selected()
    {
        await Update(Command(_hotel.Id, _grooming.Id) with { CategoryId = 12345 });

        Assert.Equal(_grooming.Id, Reload().CategoryId);
    }

    [Fact]
    public async Task Hotel_syncs_bath_meds_and_camera_extras()
    {
        await Update(Command(_hotel.Id));

        var extras = Extras();
        Assert.Equal(20000, extras.Single(e => HotelCoreExtras.MatchesBath(e.Name)).Price);
        Assert.Equal(5000, extras.Single(e => HotelCoreExtras.MatchesMeds(e.Name)).Price);
        Assert.Equal(8000, extras.Single(e => HotelPrivateCameraExtra.Matches(e.Name)).Price);
    }

    [Fact]
    public async Task Non_hotel_does_not_get_hotel_extras()
    {
        await Update(Command(_grooming.Id));

        Assert.DoesNotContain(Extras(), e => HotelCoreExtras.IsManagedHotelExtra(e.Name) && e.IsActive);
    }

    [Fact]
    public async Task Rejects_when_no_active_category_is_selected()
    {
        var error = await Update(Command(_inactive.Id, 999));

        Assert.Equal(UpdateBusinessError.NoCategory, error);
        Assert.Equal("Original", Reload().BusinessName);
    }

    [Fact]
    public async Task Invalid_phone_saves_nothing()
    {
        var error = await Update(Command(_grooming.Id) with { Phone = "abc" });

        Assert.Equal(UpdateBusinessError.PhoneInvalid, error);
        var saved = Reload();
        Assert.Equal("Original", saved.BusinessName);
        Assert.Equal("3000000000", saved.Phone);
    }

    [Fact]
    public async Task Unknown_business_is_not_found()
    {
        Assert.Equal(UpdateBusinessError.NotFound, await Update(Command(_grooming.Id) with { BusinessId = 999_999 }));
    }

    [Fact]
    public async Task Amenities_are_appended_in_order_with_default_icon()
    {
        var handler = new AddAmenityHandler(_database.CreateContext());

        Assert.True(await handler.HandleAsync(new AddAmenityCommand(_business.Id, " Wifi ", null)));
        Assert.True(await handler.HandleAsync(new AddAmenityCommand(_business.Id, "Parqueadero", "🚗")));
        Assert.False(await handler.HandleAsync(new AddAmenityCommand(_business.Id, " ", "x")));

        using var db = _database.CreateContext();
        var amenities = db.Amenities.AsNoTracking().OrderBy(a => a.SortOrder).ToList();
        Assert.Equal(["Wifi", "Parqueadero"], amenities.Select(a => a.Label));
        Assert.Equal([0, 1], amenities.Select(a => a.SortOrder));
        Assert.Equal(AddAmenityHandler.DefaultIcon, amenities[0].Icon);
    }

    [Fact]
    public async Task Extras_skip_blank_and_hotel_managed_names_and_clamp_negative_price()
    {
        var handler = new AddExtraHandler(_database.CreateContext());

        Assert.True(await handler.HandleAsync(new AddExtraCommand(_business.Id, " Perfume ", -5)));
        Assert.False(await handler.HandleAsync(new AddExtraCommand(_business.Id, "", 10)));
        Assert.False(await handler.HandleAsync(new AddExtraCommand(_business.Id, HotelPrivateCameraExtra.Name, 10)));

        var extra = Assert.Single(Extras());
        Assert.Equal("Perfume", extra.Name);
        Assert.Equal(0, extra.Price);
    }

    [Fact]
    public async Task Services_are_priced_by_size_step_and_nights_last_a_day()
    {
        var handler = new AddServiceHandler(_database.CreateContext());
        var step = HotelCoreExtras.SizeStepFor("CO");

        Assert.True(await handler.HandleAsync(new AddServiceCommand(_business.Id, " Noche ", AddServiceHandler.NightUnit, 50000, "CO")));
        Assert.False(await handler.HandleAsync(new AddServiceCommand(_business.Id, "Gratis", "sesion", 0, "CO")));

        using var db = _database.CreateContext();
        var service = db.Services.AsNoTracking().Single(s => s.GroomerId == _business.Id);
        Assert.Equal("Noche", service.Name);
        Assert.Equal(50000, service.PriceSmall);
        Assert.Equal(50000 + step, service.PriceMedium);
        Assert.Equal(50000 + step * 3, service.PriceGiant);
        Assert.Equal(1440, service.DurationMinutes);
    }
}
