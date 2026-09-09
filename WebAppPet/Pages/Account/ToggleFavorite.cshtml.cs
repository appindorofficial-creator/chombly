using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Account;

[IgnoreAntiforgeryToken]
public class ToggleFavoriteModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public ToggleFavoriteModel(AppDbContext db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    public IActionResult OnGet(int? groomerId, string? returnUrl = null)
        => Redirect(SafeReturn(returnUrl, groomerId));

    public async Task<IActionResult> OnPostAsync(int groomerId, string? returnUrl = null)
    {
        var wantsJson = string.Equals(Request.Headers.Accept, "application/json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Request.Query["format"], "json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        if (_auth.CurrentUserId is not int userId)
        {
            if (wantsJson)
                return new JsonResult(new { ok = false, error = "login" }) { StatusCode = 401 };

            var loginReturn = Url.Page("/Groomers/Details", new { id = groomerId }) ?? "/Groomers/Details/" + groomerId;
            return RedirectToPage("/Account/Login", new { returnUrl = loginReturn });
        }

        var exists = await _db.Groomers.AnyAsync(g => g.Id == groomerId && g.IsActive);
        if (!exists)
        {
            if (wantsJson)
                return new JsonResult(new { ok = false, error = "notfound" }) { StatusCode = 404 };
            return Redirect(SafeReturn(returnUrl, groomerId));
        }

        var fav = await _db.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.GroomerId == groomerId);
        var isFavorite = false;
        if (fav == null)
        {
            _db.Favorites.Add(new Favorite { UserId = userId, GroomerId = groomerId });
            isFavorite = true;
        }
        else
        {
            _db.Favorites.Remove(fav);
            isFavorite = false;
        }

        await _db.SaveChangesAsync();

        if (wantsJson)
            return new JsonResult(new { ok = true, isFavorite, groomerId });

        return Redirect(SafeReturn(returnUrl, groomerId));
    }

    private string SafeReturn(string? returnUrl, int? groomerId)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl)
            && Uri.TryCreate(returnUrl, UriKind.Relative, out _)
            && returnUrl.StartsWith('/')
            && !returnUrl.StartsWith("//", StringComparison.Ordinal))
            return returnUrl;

        if (groomerId is int id)
            return Url.Page("/Groomers/Details", new { id }) ?? $"/Groomers/Details/{id}";

        return Url.Page("/Account/Favorites") ?? "/Account/Favorites";
    }
}
