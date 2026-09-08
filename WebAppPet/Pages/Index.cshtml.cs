using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;

namespace WebAppPet.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db) => _db = db;

    public string City { get; set; } = string.Empty;
    public List<ServiceCategory> Categories { get; set; } = new();
    public List<GroomerProfile> Featured { get; set; } = new();
    public List<string> PopularServices { get; set; } = new();

    public async Task OnGetAsync(string? city)
    {
        City = city ?? string.Empty;

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

        PopularServices = await _db.Services
            .Where(s => s.Groomer.IsActive && s.Groomer.PublishStatus == BusinessPublishStatus.Approved)
            .GroupBy(s => s.Name)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Take(8)
            .ToListAsync();

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
    }

    public string TypeLabel(GroomerType t) => t switch
    {
        GroomerType.Mobile => CatalogLocalizer.Loc("Móvil", "Mobile"),
        GroomerType.InHome => CatalogLocalizer.Loc("A domicilio", "In-home"),
        _ => CatalogLocalizer.Loc("Salón", "Salon")
    };
}