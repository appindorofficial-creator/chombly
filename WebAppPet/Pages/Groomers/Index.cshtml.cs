using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
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
        }

        var query = _db.Groomers
            .Include(g => g.Category)
            .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved)
            .AsQueryable();

        if (ActiveCategory != null)
            query = query.Where(g => g.CategoryId == ActiveCategory.Id);

        if (!string.IsNullOrWhiteSpace(Q))
            query = query.Where(g => g.BusinessName.Contains(Q) || g.About.Contains(Q));

        if (!string.IsNullOrWhiteSpace(City))
            query = query.Where(g => g.City.Contains(City));

        if (!string.IsNullOrWhiteSpace(Type) && Enum.TryParse<GroomerType>(Type, true, out var t))
            query = query.Where(g => g.Type == t);

        if (Senior) query = query.Where(g => g.AcceptsSeniorPets);
        if (Anxious) query = query.Where(g => g.AcceptsAnxiousPets);

        if (!string.IsNullOrWhiteSpace(Service))
            query = query.Where(g => g.Services.Any(s => s.Name.Contains(Service)));

        var list = await query
            .OrderByDescending(g => g.Rating)
            .ThenBy(g => g.StartingPrice)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(Species) && PetSpecies.IsKnown(Species))
            list = list.Where(g => g.AcceptsSpecies(Species)).ToList();

        var todayMap = await _availability.TodayMapAsync(list.Select(g => g.Id));

        Results = list.Select(g =>
        {
            string? distance = null;
            if (userLat != null && userLng != null && (g.Latitude != 0 || g.Longitude != 0))
            {
                var miles = GeoHelper.MilesBetween(userLat.Value, userLng.Value, g.Latitude, g.Longitude);
                distance = GeoHelper.FormatMilesAway(miles);
            }

            var available = todayMap.GetValueOrDefault(g.Id, true);
            return new BusinessCardVm
            {
                Business = g,
                DistanceLabel = distance,
                AvailableToday = available
            };
        }).ToList();

        if (AvailableToday)
            Results = Results.Where(r => r.AvailableToday).ToList();

        if (HasUserLocation)
            Results = Results.OrderBy(r => r.DistanceLabel == null).ThenBy(r => ParseMiles(r.DistanceLabel)).ToList();
    }

    private static double ParseMiles(string? label)
    {
        if (string.IsNullOrEmpty(label)) return double.MaxValue;
        var part = label.Replace("A ", "").Replace(" mi de ti", "").Trim();
        return double.TryParse(part, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var m) ? m : double.MaxValue;
    }

    public class BusinessCardVm
    {
        public GroomerProfile Business { get; set; } = null!;
        public string? DistanceLabel { get; set; }
        public bool AvailableToday { get; set; }
    }
}
