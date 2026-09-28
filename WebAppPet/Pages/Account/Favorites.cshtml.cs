using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Favorites.GetFavorites;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Account;

public class FavoritesModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetFavoritesHandler _getFavorites;

    public FavoritesModel(AuthService auth, GetFavoritesHandler getFavorites)
    {
        _auth = auth;
        _getFavorites = getFavorites;
    }

    public List<GroomerProfile> Groomers { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        Groomers = await _getFavorites.HandleAsync(new GetFavoritesQuery(userId), ct);
        return Page();
    }
}
