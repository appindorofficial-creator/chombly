using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Account;

public class FavoritesModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public FavoritesModel(AppDbContext db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    public List<GroomerProfile> Groomers { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        Groomers = await _db.Favorites
            .Where(f => f.UserId == userId)
            .Include(f => f.Groomer)!.ThenInclude(g => g.Category)
            .Select(f => f.Groomer)
            .ToListAsync();

        return Page();
    }
}
