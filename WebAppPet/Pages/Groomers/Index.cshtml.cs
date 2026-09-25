using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Businesses.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Groomers;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly AvailabilityService _availability;

    public IndexModel(AppDbContext db, AuthService auth, AvailabilityService availability)
    {
        _db = db;
        _auth = auth;
        _availability = availability;
    }

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Type { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Service { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? City { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Species { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? CustomType { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Category { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Senior { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Anxious { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool AvailableToday { get; set; }

    public ServiceCategory? ActiveCategory { get; set; }
    public List<ServiceCategory> Categories { get; set; } = new();
    public List<BusinessCardVm> Results { get; set; } = new();
    public bool HasUserLocation { get; set; }
    public HashSet<int> FavoriteIds { get; set; } = new();

    public async Task OnGetAsync()
    {
        if (!string.IsNullOrWhiteSpace(CustomType))
            CustomType = CustomType.Trim();

        Categories = await _db.Categories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync();

        if (!string.IsNullOrWhiteSpace(Category))
            ActiveCategory = Categories.FirstOrDefault(c => c.Slug.Equals(Category, StringComparison.OrdinalIgnoreCase));

        double? userLat = null, userLng = null;
        if (_auth.CurrentUserId is int uid)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid);
            if (user?.Latitude != null && user.Longitude != null)
            {
                userLat = user.Latitude;
                userLng = user.Longitude;
                HasUserLocation = true;
            }

            FavoriteIds = (await _db.Favorites.AsNoTracking()
                    .Where(f => f.UserId == uid)
                    .Select(f => f.GroomerId)
                    .ToListAsync())
                .ToHashSet();
        }

        var query = _db.Groomers
            .Include(g => g.Category)
            .Include(g => g.Services)
            .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved)
            .AsQueryable();

        var list = await query.ToListAsync();
        list = BusinessMarketResolver.FilterHomeMarket(list, AppTimeZones.CurrentCountryCode).ToList();

        if (ActiveCategory != null)
            list = list.Where(g => g.OffersCategory(ActiveCategory.Id)).ToList();

        if (!string.IsNullOrWhiteSpace(Q))
            list = list.Where(g =>
                g.BusinessName.Contains(Q, StringComparison.OrdinalIgnoreCase)
                || g.About.Contains(Q, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(City))
            list = list.Where(g => g.City.Contains(City, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(Type) && Enum.TryParse<GroomerType>(Type, true, out var t))
            list = list.Where(g => g.Type == t).ToList();

        if (Senior) list = list.Where(g => g.AcceptsSeniorPets).ToList();
        if (Anxious) list = list.Where(g => g.AcceptsAnxiousPets).ToList();

        if (!string.IsNullOrWhiteSpace(Service))
            list = list.Where(g => g.Services.Any(s => s.Name.Contains(Service, StringComparison.OrdinalIgnoreCase))).ToList();

        list = list
            .OrderByDescending(g => g.Rating)
            .ThenBy(g => g.StartingPrice)
            .ToList();

        if (!string.IsNullOrWhiteSpace(Species) && PetSpecies.IsKnown(Species))
            list = list.Where(g => g.AcceptsSpecies(Species)).ToList();

        var todayMap = await _availability.TodayMapAsync(list.Select(g => g.Id));

        Results = list.Select(g =>
        {
            string? distance = null;
            double? sortKm = null;
            if (userLat != null && userLng != null && (g.Latitude != 0 || g.Longitude != 0))
            {
                sortKm = GeoHelper.KmBetween(userLat.Value, userLng.Value, g.Latitude, g.Longitude);
                distance = GeoHelper.FormatDistanceOrPlace(sortKm, g.City, g.Address);
            }

            var available = todayMap.GetValueOrDefault(g.Id, false);
            return new BusinessCardVm
            {
                Business = g,
                DistanceLabel = distance,
                SortKm = sortKm,
                AvailableToday = available
            };
        }).ToList();

        if (AvailableToday)
            Results = Results.Where(r => r.AvailableToday).ToList();

        if (HasUserLocation)
            Results = Results.OrderBy(r => r.SortKm ?? double.MaxValue).ToList();
    }

    public class BusinessCardVm
    {
        public GroomerProfile Business { get; set; } = null!;
        public string? DistanceLabel { get; set; }
        public double? SortKm { get; set; }
        public bool AvailableToday { get; set; }
    }
}
