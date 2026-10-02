using WebAppPet.Application.Bookings.FindNextFreeStart;
using WebAppPet.Application.Bookings.GetActiveExtras;
using WebAppPet.Application.Bookings.GetBookableProvider;
using WebAppPet.Application.Bookings.GetCategoryBookingContext;
using WebAppPet.Application.Bookings.GetOccupiedSlots;
using WebAppPet.Application.Businesses.EnsureHotelCoreExtras;
using WebAppPet.Application.Businesses.SearchHotels;
using WebAppPet.Application.Common;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Bookings;

public class CategoryBookingQueriesTests : IDisposable
{
    private static readonly string[] Slots = { "9:00 AM", "10:00 AM", "11:00 AM" };

    /// <summary>A Saturday far enough ahead that none of its slots are in the past.</summary>
    private static readonly DateTime FutureSaturday = Next(DayOfWeek.Saturday, AppTimeZones.TodayLocalDate().AddDays(14));

    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly GroomerProfile _business;
    private readonly AppUser _client;

    public CategoryBookingQueriesTests()
    {
        _db = _database.CreateContext();
        _business = TestData.AddBusiness(_db);
        _client = TestData.AddUser(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private static DateTime Next(DayOfWeek dayOfWeek, DateTime from)
    {
        var day = from.Date;
        while (day.DayOfWeek != dayOfWeek) day = day.AddDays(1);
        return day;
    }

    private static DateTime Utc(DateTime localDay, int hour) =>
        AppTimeZones.LocalDateAndTimeToUtc(localDay, TimeSpan.FromHours(hour));

    private void AddAppointmentAt(DateTime localDay, int hour, AppointmentStatus status = AppointmentStatus.Confirmed, GroomerProfile? business = null) =>
        TestData.AddAppointment(_db, _client, business ?? _business, status, Utc(localDay, hour));

    private ServiceCategory AddCategory(string slug, bool isActive = true)
    {
        var category = new ServiceCategory { Slug = slug, Name = slug, IsActive = isActive };
        _db.Categories.Add(category);
        _db.SaveChanges();
        return category;
    }

    private GroomerProfile AddHotel(string city, int? categoryId, string? extraCategoryIds = null, bool featured = false, double rating = 0)
    {
        var hotel = TestData.AddBusiness(_db);
        hotel.City = city;
        hotel.CategoryId = categoryId;
        hotel.ExtraCategoryIds = extraCategoryIds;
        hotel.IsFeatured = featured;
        hotel.Rating = rating;
        _db.SaveChanges();
        return hotel;
    }

    private Task<FreeStart?> NextFree(DateTime day, string? preferred, DateTime? nowUtc = null) =>
        new FindNextFreeStartHandler(_db).HandleAsync(
            new FindNextFreeStartQuery(_business.Id, day, preferred, Slots, nowUtc ?? DateTime.UtcNow));

    [Fact]
    public async Task Context_without_a_client_only_has_the_active_category()
    {
        var hotel = AddCategory("hotel");
        AddCategory("daycare", isActive: false);
        TestData.AddPet(_db, _client);
        TestData.AddCard(_db, _client);

        var context = await new GetCategoryBookingContextHandler(_db).HandleAsync(new GetCategoryBookingContextQuery("hotel", null));
        var inactive = await new GetCategoryBookingContextHandler(_db).HandleAsync(new GetCategoryBookingContextQuery("daycare", null));

        Assert.Equal(hotel.Id, context.Category?.Id);
        Assert.Null(context.ClientLatitude);
        Assert.Empty(context.Pets);
        Assert.Empty(context.Payments);
        Assert.Null(inactive.Category);
    }

    [Fact]
    public async Task Context_loads_the_clients_location_pets_by_name_and_default_card_first()
    {
        _client.Latitude = 4.65;
        _client.Longitude = -74.05;
        _db.SaveChanges();
        var zeus = TestData.AddPet(_db, _client);
        zeus.Name = "Zeus";
        var luna = TestData.AddPet(_db, _client);
        luna.Name = "Luna";
        _db.SaveChanges();
        TestData.AddPet(_db, TestData.AddUser(_db));
        var other = TestData.AddCard(_db, _client, "1111", isDefault: false);
        var main = TestData.AddCard(_db, _client, "4242", isDefault: true);

        var context = await new GetCategoryBookingContextHandler(_db).HandleAsync(new GetCategoryBookingContextQuery("walkers", _client.Id));

        Assert.Null(context.Category);
        Assert.Equal(4.65, context.ClientLatitude);
        Assert.Equal(-74.05, context.ClientLongitude);
        Assert.Equal(new[] { luna.Id, zeus.Id }, context.Pets.Select(p => p.Id));
        Assert.Equal(new[] { main.Id, other.Id }, context.Payments.Select(p => p.Id));
    }

    [Fact]
    public async Task Bookable_provider_includes_services_and_skips_inactive_businesses()
    {
        var service = TestData.AddService(_db, _business);
        var inactive = TestData.AddBusiness(_db);
        inactive.IsActive = false;
        _db.SaveChanges();

        var provider = await new GetBookableProviderHandler(_db).HandleAsync(new GetBookableProviderQuery(_business.Id));

        Assert.NotNull(provider);
        Assert.Equal(service.Id, Assert.Single(provider.Services).Id);
        Assert.Null(await new GetBookableProviderHandler(_db).HandleAsync(new GetBookableProviderQuery(inactive.Id)));
        Assert.Null(await new GetBookableProviderHandler(_db).HandleAsync(new GetBookableProviderQuery(999_999)));
    }

    [Fact]
    public async Task Active_extras_are_only_the_active_ones_of_the_given_businesses()
    {
        var other = TestData.AddBusiness(_db);
        var perfume = TestData.AddExtra(_db, _business, "Perfume", 5);
        var nails = TestData.AddExtra(_db, other, "Uñas", 8);
        TestData.AddExtra(_db, TestData.AddBusiness(_db), "Otro", 3);
        TestData.AddExtra(_db, _business, "Viejo", 1).IsActive = false;
        _db.SaveChanges();

        var extras = await new GetActiveExtrasHandler(_db).HandleAsync(new GetActiveExtrasQuery(new[] { _business.Id, other.Id }));

        Assert.Equal(new[] { perfume.Id, nails.Id }.Order(), extras.Select(e => e.Id).Order());
        Assert.Empty(await new GetActiveExtrasHandler(_db).HandleAsync(new GetActiveExtrasQuery(Array.Empty<int>())));
    }

    [Fact]
    public async Task Occupied_slots_are_the_ones_starting_with_a_non_cancelled_appointment_that_day()
    {
        AddAppointmentAt(FutureSaturday, 9);
        AddAppointmentAt(FutureSaturday, 10, AppointmentStatus.Cancelled);
        AddAppointmentAt(FutureSaturday, 11, business: TestData.AddBusiness(_db));
        AddAppointmentAt(FutureSaturday.AddDays(1), 11);

        var occupied = await new GetOccupiedSlotsHandler(_db).HandleAsync(new GetOccupiedSlotsQuery(_business.Id, FutureSaturday, Slots));

        Assert.Equal(new[] { "9:00 AM" }, occupied);
        Assert.Contains("9:00 am", occupied);
    }

    [Fact]
    public async Task Next_free_start_is_the_preferred_slot_when_it_is_free()
    {
        var free = await NextFree(FutureSaturday, "10:00 AM");

        Assert.NotNull(free);
        Assert.Equal(new FreeStart(Utc(FutureSaturday, 10), FutureSaturday, "10:00 AM"), free);
    }

    [Fact]
    public async Task Next_free_start_moves_to_the_next_slot_ignoring_cancelled_and_other_businesses()
    {
        AddAppointmentAt(FutureSaturday, 10);
        AddAppointmentAt(FutureSaturday, 11, AppointmentStatus.Cancelled);
        AddAppointmentAt(FutureSaturday, 11, business: TestData.AddBusiness(_db));

        var free = await NextFree(FutureSaturday, "10:00 AM");

        Assert.Equal("11:00 AM", free?.Slot);
        Assert.Equal(Utc(FutureSaturday, 11), free?.StartUtc);
    }

    [Fact]
    public async Task Next_free_start_skips_sunday_and_starts_the_following_day_from_the_first_slot()
    {
        AddAppointmentAt(FutureSaturday, 11);
        var monday = FutureSaturday.AddDays(2);

        var free = await NextFree(FutureSaturday, "11:00 AM");

        Assert.Equal(new FreeStart(Utc(monday, 9), monday, "9:00 AM"), free);
    }

    [Fact]
    public async Task Next_free_start_skips_past_slots_and_starts_from_the_first_slot_when_preferred_is_unknown()
    {
        var free = await NextFree(FutureSaturday, "7:00 PM", nowUtc: Utc(FutureSaturday, 9));

        Assert.Equal("10:00 AM", free?.Slot);
    }

    [Fact]
    public async Task Next_free_start_is_null_when_the_whole_window_is_taken()
    {
        for (var offset = 0; offset < FindNextFreeStartHandler.SearchDays; offset++)
        {
            var day = FutureSaturday.AddDays(offset);
            if (day.DayOfWeek == DayOfWeek.Sunday) continue;
            foreach (var hour in new[] { 9, 10, 11 }) AddAppointmentAt(day, hour);
        }

        Assert.Null(await NextFree(FutureSaturday, "9:00 AM"));
    }

    [Fact]
    public async Task Hotels_offer_the_category_in_the_home_market_featured_then_by_rating()
    {
        var hotel = AddCategory("hotel");
        var grooming = AddCategory("grooming");
        var low = AddHotel("Bogotá", hotel.Id, rating: 3);
        var featured = AddHotel("Medellín", hotel.Id, featured: true, rating: 1);
        var high = AddHotel("Cali", hotel.Id, rating: 5);
        var asExtra = AddHotel("Neiva", grooming.Id, extraCategoryIds: $"99, {hotel.Id}", rating: 4);
        AddHotel("Bogotá", grooming.Id);
        AddHotel("Miami", hotel.Id);
        AddHotel("Bogotá", hotel.Id).PublishStatus = BusinessPublishStatus.PendingReview;
        AddHotel("Bogotá", hotel.Id).IsActive = false;
        _db.SaveChanges();

        var hotels = await new SearchHotelsHandler(_db).HandleAsync(new SearchHotelsQuery(hotel.Id, "CO"));

        Assert.Equal(new[] { featured.Id, high.Id, asExtra.Id, low.Id }, hotels.Select(h => h.Id));
    }

    [Fact]
    public async Task Hotels_without_a_hotel_category_match_the_primary_category_slug()
    {
        var hotel = AddCategory("hotel", isActive: false);
        var grooming = AddCategory("grooming");
        var primary = AddHotel("Bogotá", hotel.Id);
        AddHotel("Bogotá", grooming.Id, extraCategoryIds: hotel.Id.ToString());

        var hotels = await new SearchHotelsHandler(_db).HandleAsync(new SearchHotelsQuery(null, "CO"));

        Assert.Equal(primary.Id, Assert.Single(hotels).Id);
    }

    [Fact]
    public async Task Ensure_core_extras_adds_bath_and_meds_once_without_touching_existing_prices()
    {
        var custom = TestData.AddExtra(_db, _business, HotelCoreExtras.BathName, 0);
        var other = TestData.AddBusiness(_db);
        var handler = new EnsureHotelCoreExtrasHandler(_db);

        await handler.HandleAsync(new EnsureHotelCoreExtrasCommand(new[] { _business.Id, other.Id }));
        await handler.HandleAsync(new EnsureHotelCoreExtrasCommand(new[] { _business.Id, other.Id }));

        var mine = _db.ServiceExtras.Where(e => e.GroomerId == _business.Id).ToList();
        Assert.Equal(2, mine.Count);
        Assert.Equal(0, mine.Single(e => e.Id == custom.Id).Price);
        Assert.Equal(HotelCoreExtras.MedsDefaultPriceCop, mine.Single(e => HotelCoreExtras.MatchesMeds(e.Name)).Price);
        Assert.Equal(2, _db.ServiceExtras.Count(e => e.GroomerId == other.Id));
    }
}
