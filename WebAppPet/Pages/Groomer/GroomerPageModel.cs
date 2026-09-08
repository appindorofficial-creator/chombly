using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
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

        ViewData["ProviderCategory"] = Profile.Category?.Name;
        return null;
    }
}
