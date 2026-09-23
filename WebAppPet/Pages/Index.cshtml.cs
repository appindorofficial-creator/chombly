using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly IStringLocalizer<SharedResource> _L;

    public IndexModel(AppDbContext db, AuthService auth, IStringLocalizer<SharedResource> L)
    {
        _db = db;
        _auth = auth;
        _L = L;
    }

    /// <summary>Guest browse from Welcome “Explorar”. Without this, anonymous /Index goes to Welcome.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Browse { get; set; }

    /// <summary>Welcome Negocios “Explorar servicios” — business-oriented home instead of family marketplace.</summary>
    [BindProperty(SupportsGet = true)]
    public string? For { get; set; }

    public bool IsBusinessExplore =>
        string.Equals(For, "business", StringComparison.OrdinalIgnoreCase);

    public string City { get; set; } = string.Empty;
    public string? GreetingName { get; set; }
    public bool IsGuest { get; set; }
    public List<ServiceCategory> Categories { get; set; } = new();
    public List<GroomerProfile> Featured { get; set; } = new();
    public List<string> PopularServices { get; set; } = new();
    public HashSet<int> FavoriteIds { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string? city)
    {
        IsGuest = !_auth.IsAuthenticated;
        if (_auth.IsAdmin)
            return RedirectToPage("/Admin/Approvals");
        // Dual-role (Indor-style): only force business home when shell is business.
        if (_auth.IsBusinessShell)
            return RedirectToPage("/Groomer/Dashboard");
        if (IsBusinessExplore)
        {
            BusinessExploreMode.Enable(Response);
            return RedirectToPage("/Business/Explore/Index");
        }

        if (IsGuest && !Browse)
            return Redirect("/Welcome");

        if (IsGuest)
            ViewData["OnboardShowNotif"] = false;

        City = city ?? string.Empty;
        GreetingName = FirstName(_auth.IsAuthenticated ? User.Identity?.Name : null);

        Categories = await _db.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();

        var homeCountry = AppTimeZones.CurrentCountryCode;
        Featured = BusinessMarketResolver.FilterHomeMarket(
                await _db.Groomers
                    .Include(g => g.Category)
                    .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved && g.IsFeatured)
                    .OrderByDescending(g => g.Rating)
                    .ToListAsync(),
                homeCountry)
            .Take(6)
            .ToList();

        if (Featured.Count == 0)
        {
            Featured = BusinessMarketResolver.FilterHomeMarket(
                    await _db.Groomers
                        .Include(g => g.Category)
                        .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved)
                        .OrderByDescending(g => g.Rating)
                        .ThenBy(g => g.BusinessName)
                        .ToListAsync(),
                    homeCountry)
                .Take(6)
                .ToList();
        }

        if (_auth.CurrentUserId is int uid)
        {
            FavoriteIds = (await _db.Favorites.AsNoTracking()
                    .Where(f => f.UserId == uid)
                    .Select(f => f.GroomerId)
                    .ToListAsync())
                .ToHashSet();
        }

        var homeServices = await _db.Services
            .Include(s => s.Groomer)
            .Where(s => s.Groomer.IsActive && s.Groomer.PublishStatus == BusinessPublishStatus.Approved)
            .ToListAsync();
        PopularServices = homeServices
            .Where(s => BusinessMarketResolver.MatchesHomeMarket(s.Groomer, homeCountry))
            .GroupBy(s => CanonicalPopularServiceName(s.Name))
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Take(8)
            .ToList();

        if (PopularServices.Count == 0)
        {
            PopularServices = new List<string>
            {
                "Baño y cepillado",
                "Grooming completo",
                "Deshedding",
                "Corte",
                "Consulta general",
                "Día completo"
            };
        }

        return Page();
    }

    /// <summary>Merge near-duplicate grooming labels so Home chips don't show the same service twice.</summary>
    private static string CanonicalPopularServiceName(string? name)
    {
        var n = (name ?? string.Empty).Trim();
        if (n.Length == 0) return n;
        if (string.Equals(n, "Baño y secado", StringComparison.OrdinalIgnoreCase)
            || string.Equals(n, "Bath & dry", StringComparison.OrdinalIgnoreCase)
            || string.Equals(n, "Bath and dry", StringComparison.OrdinalIgnoreCase)
            || string.Equals(n, "Bath y secado", StringComparison.OrdinalIgnoreCase))
            return "Baño y cepillado";
        return n;
    }

    public string TypeLabel(GroomerType t) => t switch
    {
        GroomerType.Mobile => _L["Type_Mobile"].Value,
        GroomerType.InHome => _L["Type_InHome"].Value,
        _ => _L["Type_Salon"].Value
    };

    private static string? FirstName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return null;
        var part = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0];
        return string.IsNullOrWhiteSpace(part) ? null : part;
    }
}
