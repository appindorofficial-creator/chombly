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

    public string City { get; set; } = string.Empty;
    public string? GreetingName { get; set; }
    public List<ServiceCategory> Categories { get; set; } = new();
    public List<GroomerProfile> Featured { get; set; } = new();
    public List<string> PopularServices { get; set; } = new();
    public HashSet<int> FavoriteIds { get; set; } = new();

    public async Task OnGetAsync(string? city)
    {
        City = city ?? string.Empty;
        GreetingName = FirstName(_auth.IsAuthenticated ? User.Identity?.Name : null);

        Categories = await _db.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();

        Featured = await _db.Groomers
            .Include(g => g.Category)
            .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved && g.IsFeatured)
            .OrderByDescending(g => g.Rating)
            .Take(6)
            .ToListAsync();

        if (Featured.Count == 0)
        {
            Featured = await _db.Groomers
                .Include(g => g.Category)
                .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved)
                .OrderByDescending(g => g.Rating)
                .ThenBy(g => g.BusinessName)
                .Take(6)
                .ToListAsync();
        }

        if (_auth.CurrentUserId is int uid)
        {
            FavoriteIds = (await _db.Favorites.AsNoTracking()
                    .Where(f => f.UserId == uid)
                    .Select(f => f.GroomerId)
                    .ToListAsync())
                .ToHashSet();
        }

        PopularServices = await _db.Services
            .Where(s => s.Groomer.IsActive && s.Groomer.PublishStatus == BusinessPublishStatus.Approved)
            .GroupBy(s => s.Name)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Take(8)
            .ToListAsync();

        if (PopularServices.Count == 0)
        {
            // Claves en español (valor en BD); CatalogLocalizer.Text las traduce en UI.
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
