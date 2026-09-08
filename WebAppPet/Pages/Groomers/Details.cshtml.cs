using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Groomers;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public DetailsModel(AppDbContext db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    public GroomerProfile? Groomer { get; set; }
    public List<GroomerService> Services { get; set; } = new();
    public List<BusinessAmenity> Amenities { get; set; } = new();
    public List<ServiceExtra> Extras { get; set; } = new();
    public List<Review> Reviews { get; set; } = new();
    public List<GroomerPhoto> Photos { get; set; } = new();
    public bool IsFavorite { get; set; }

    /// <summary>Popular / search service name to pre-select on the booking page.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Service { get; set; }

    /// <summary>Resolved catalog service id when <see cref="Service"/> matches this groomer's offerings.</summary>
    public int? PrefillServiceId { get; set; }

    public int StarPercent(int star)
    {
        if (Reviews.Count == 0) return 0;
        return (int)Math.Round(100.0 * Reviews.Count(r => r.Rating == star) / Reviews.Count);
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Groomer = await _db.Groomers
            .Include(g => g.Category)
            .FirstOrDefaultAsync(g => g.Id == id && g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved);
        if (Groomer == null) return Page();

        Services = await _db.Services.Where(s => s.GroomerId == id).OrderBy(s => s.PriceSmall).ToListAsync();
        Amenities = await _db.Amenities.Where(a => a.GroomerId == id).OrderBy(a => a.SortOrder).ToListAsync();
        Extras = await _db.ServiceExtras.Where(e => e.GroomerId == id && e.IsActive).OrderBy(e => e.Price).ToListAsync();
        Photos = await _db.GroomerPhotos.Where(p => p.GroomerId == id).ToListAsync();
        Reviews = await _db.Reviews.Include(r => r.Client)
            .Where(r => r.GroomerId == id)
            .OrderByDescending(r => r.CreatedAt)
            .Take(10)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(Service))
        {
            var match = Services.FirstOrDefault(s =>
                            s.Name.Equals(Service, StringComparison.OrdinalIgnoreCase))
                        ?? Services.FirstOrDefault(s =>
                            s.Name.Contains(Service, StringComparison.OrdinalIgnoreCase)
                            || Service.Contains(s.Name, StringComparison.OrdinalIgnoreCase));
            PrefillServiceId = match?.Id;
        }

        if (_auth.CurrentUserId is int userId)
            IsFavorite = await _db.Favorites.AnyAsync(f => f.UserId == userId && f.GroomerId == id);

        return Page();
    }

    public async Task<IActionResult> OnPostToggleFavoriteAsync(int id)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        var fav = await _db.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.GroomerId == id);
        if (fav == null)
            _db.Favorites.Add(new Favorite { UserId = userId, GroomerId = id });
        else
            _db.Favorites.Remove(fav);

        await _db.SaveChangesAsync();
        return RedirectToPage(new { id, service = Service });
    }

    public string UnitLabel(string? billingUnit) => CatalogLocalizer.Text(billingUnit switch
    {
        "noche" => "/ noche",
        "dia" => "/ día",
        "visita" => "/ visita",
        _ => "/ sesión"
    });
}