using WebAppPet.Application.Businesses.SearchBusinesses;
using WebAppPet.Application.Businesses.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Businesses;

public class SearchBusinessesHandlerTests : IDisposable
{
    private static readonly DateTime Monday = new(2026, 9, 28);

    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly ServiceCategory _walkers;
    private readonly ServiceCategory _trainers;

    public SearchBusinessesHandlerTests()
    {
        _db = _database.CreateContext();
        _walkers = AddCategory("walkers");
        _trainers = AddCategory("trainers");
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private ServiceCategory AddCategory(string slug)
    {
        var category = new ServiceCategory { Slug = slug, Name = slug, IsActive = true };
        _db.Categories.Add(category);
        _db.SaveChanges();
        return category;
    }

    private GroomerProfile AddListing(
        string name,
        ServiceCategory? category = null,
        string country = "CO",
        string species = PetSpecies.Dog + "," + PetSpecies.Cat,
        double rating = 4,
        bool featured = false,
        double lat = 0,
        double lng = 0,
        BusinessPublishStatus status = BusinessPublishStatus.Approved,
        bool active = true,
        string? extraCategoryIds = null)
    {
        var owner = TestData.AddUser(_db, UserRole.Groomer);
        var business = new GroomerProfile
        {
            UserId = owner.Id,
            BusinessName = name,
            CategoryId = (category ?? _walkers).Id,
            ExtraCategoryIds = extraCategoryIds,
            LicenseCountry = country,
            AcceptedSpecies = species,
            Rating = rating,
            IsFeatured = featured,
            Latitude = lat,
            Longitude = lng,
            PublishStatus = status,
            IsActive = active
        };
        _db.Groomers.Add(business);
        _db.SaveChanges();
        return business;
    }

    private Task<List<BusinessMatch>> Search(SearchBusinessesQuery query)
    {
        var db = _database.CreateContext();
        return new SearchBusinessesHandler(db, new AvailabilityService(db)).HandleAsync(query);
    }

    private static SearchBusinessesQuery Walkers(params string[] petSpecies) =>
        new("walkers", "CO", petSpecies.Length == 0 ? [PetSpecies.Dog] : petSpecies);

    private static List<string> Names(IEnumerable<BusinessMatch> matches) =>
        matches.Select(m => m.Business.BusinessName).ToList();

    [Fact]
    public async Task Lists_only_approved_active_businesses_whose_primary_category_matches_in_the_home_market()
    {
        AddListing("Listed");
        AddListing("Pending", status: BusinessPublishStatus.PendingReview);
        AddListing("Inactive", active: false);
        AddListing("Trainer", category: _trainers);
        AddListing("Trainer who also walks", category: _trainers, extraCategoryIds: _walkers.Id.ToString());
        AddListing("United States", country: "US");

        var matches = await Search(Walkers());

        Assert.Equal(["Listed"], Names(matches));
    }

    [Fact]
    public async Task Without_pets_nothing_is_listed()
    {
        AddListing("Listed");

        Assert.Empty(await Search(Walkers() with { PetSpecies = [] }));
    }

    [Fact]
    public async Task Every_selected_pet_and_the_required_species_must_be_accepted()
    {
        AddListing("Dogs and cats");
        AddListing("Dogs only", species: PetSpecies.Dog);
        AddListing("Cats only", species: PetSpecies.Cat);

        Assert.Equal(["Dogs and cats"], Names(await Search(Walkers(PetSpecies.Dog, PetSpecies.Cat))));
        Assert.Equal(["Dogs and cats"], Names(await Search(Walkers(PetSpecies.Cat) with { RequiredSpecies = PetSpecies.Dog })));
    }

    [Fact]
    public async Task Open_on_uses_the_day_override_then_weekly_hours_and_hides_businesses_without_agenda()
    {
        var closedByOverride = AddListing("Closed by override");
        var openByOverride = AddListing("Open by override");
        var openWeekly = AddListing("Open weekly");
        AddListing("No agenda");

        _db.WeeklyHours.AddRange(
            new BusinessWeeklyHour { GroomerId = closedByOverride.Id, DayOfWeek = 1, IsOpen = true },
            new BusinessWeeklyHour { GroomerId = openByOverride.Id, DayOfWeek = 1, IsOpen = false },
            new BusinessWeeklyHour { GroomerId = openWeekly.Id, DayOfWeek = 1, IsOpen = true });
        _db.DayAvailabilities.AddRange(
            new BusinessDayAvailability { GroomerId = closedByOverride.Id, Day = Monday, IsAvailable = false },
            new BusinessDayAvailability { GroomerId = openByOverride.Id, Day = Monday, IsAvailable = true });
        _db.SaveChanges();

        var matches = await Search(Walkers() with { OpenOn = Monday.AddHours(15) });

        Assert.Equal(["Open by override", "Open weekly"], Names(matches).Order().ToList());
    }

    [Fact]
    public async Task Open_on_matches_the_single_business_rule()
    {
        var ids = new[]
        {
            AddListing("A").Id, AddListing("B").Id, AddListing("C").Id
        };
        _db.WeeklyHours.Add(new BusinessWeeklyHour { GroomerId = ids[0], DayOfWeek = 1, IsOpen = true });
        _db.DayAvailabilities.Add(new BusinessDayAvailability { GroomerId = ids[1], Day = Monday, IsAvailable = true });
        _db.SaveChanges();

        var availability = new AvailabilityService(_database.CreateContext());
        var open = await availability.OpenOnAsync(ids, Monday);

        foreach (var id in ids)
            Assert.Equal(await availability.IsAvailableOnAsync(id, Monday), open.Contains(id));
    }

    [Fact]
    public async Task Without_user_location_featured_come_first_then_rating()
    {
        AddListing("Rated 5", rating: 5);
        AddListing("Featured rated 3", rating: 3, featured: true);
        AddListing("Rated 4", rating: 4);

        var matches = await Search(Walkers());

        Assert.Equal(["Featured rated 3", "Rated 5", "Rated 4"], Names(matches));
        Assert.All(matches, m => Assert.Null(m.Miles));
    }

    [Fact]
    public async Task With_user_location_nearest_come_first_and_businesses_without_coordinates_last()
    {
        AddListing("No coordinates", rating: 5, featured: true);
        AddListing("Medellín", lat: 6.2442, lng: -75.5812);
        AddListing("Chapinero", lat: 4.6486, lng: -74.0628);

        var matches = await Search(Walkers() with { UserLatitude = 4.711, UserLongitude = -74.0721 });

        Assert.Equal(["Chapinero", "Medellín", "No coordinates"], Names(matches));
        Assert.StartsWith("A ", matches[0].DistanceLabel);
        Assert.Null(matches[2].DistanceLabel);
    }
}
