using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Groomer;

public abstract class GroomerPageModel : PageModel
{
    protected readonly AppDbContext Db;
    protected readonly AuthService Auth;

    protected GroomerPageModel(AppDbContext db, AuthService auth)
    {
        Db = db;
        Auth = auth;
    }

    public GroomerProfile? Profile { get; set; }
    public List<ServiceCategory> OfferedCategories { get; set; } = new();

    protected async Task<IActionResult?> LoadGroomerAsync()
    {
        if (Auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        Profile = await Db.Groomers
            .Include(g => g.Category)
            .Include(g => g.User)
            .FirstOrDefaultAsync(g => g.UserId == userId);
        if (Profile == null)
            return RedirectToPage("/Account/RegisterBusiness");

        // Entering business routes switches shell to business (keeps nav in sync).
        if (Auth.IsGroomer && Auth.IsOwnerShell)
            Auth.SetShellMode(AppShellMode.Business);

        await LoadOfferedCategoriesAsync();
        return null;
    }

    protected async Task LoadOfferedCategoriesAsync()
    {
        OfferedCategories = new();
        if (Profile == null) return;

        var ids = Profile.GetOfferedCategoryIds();
        if (ids.Count == 0) return;

        OfferedCategories = await Db.Categories.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .OrderBy(c => c.SortOrder)
            .ToListAsync();

        ViewData["ProviderCategories"] = OfferedCategories
            .Select(c => c.DisplayName())
            .ToList();
        ViewData["ProviderCategory"] = string.Join(" · ", OfferedCategories.Select(c => c.DisplayName()));
    }
}
