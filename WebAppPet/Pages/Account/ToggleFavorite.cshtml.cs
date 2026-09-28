using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Favorites.ToggleFavorite;
using WebAppPet.Services;

namespace WebAppPet.Pages.Account;

[IgnoreAntiforgeryToken]
public class ToggleFavoriteModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ToggleFavoriteHandler _toggleFavorite;

    public ToggleFavoriteModel(AuthService auth, ToggleFavoriteHandler toggleFavorite)
    {
        _auth = auth;
        _toggleFavorite = toggleFavorite;
    }

    public IActionResult OnGet(int? groomerId, string? returnUrl = null)
        => Redirect(SafeReturn(returnUrl, groomerId));

    public async Task<IActionResult> OnPostAsync(int groomerId, CancellationToken ct, string? returnUrl = null)
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

        var outcome = await _toggleFavorite.HandleAsync(new ToggleFavoriteCommand(userId, groomerId), ct);
        if (outcome == ToggleFavoriteOutcome.NotFound)
        {
            if (wantsJson)
                return new JsonResult(new { ok = false, error = "notfound" }) { StatusCode = 404 };
            return Redirect(SafeReturn(returnUrl, groomerId));
        }

        if (wantsJson)
            return new JsonResult(new { ok = true, isFavorite = outcome == ToggleFavoriteOutcome.Added, groomerId });

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
