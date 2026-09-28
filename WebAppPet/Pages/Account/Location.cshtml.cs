using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Accounts.SaveLocation;
using WebAppPet.Infrastructure.Identity;

namespace WebAppPet.Pages.Account;

[IgnoreAntiforgeryToken]
public class LocationModel : PageModel
{
    private readonly AuthService _auth;
    private readonly SaveLocationHandler _saveLocation;

    public LocationModel(AuthService auth, SaveLocationHandler saveLocation)
    {
        _auth = auth;
        _saveLocation = saveLocation;
    }

    /// <summary>
    /// Guarda lat/lng (y ciudad opcional) del navegador. Llegan como string para evitar 400 por
    /// model binding cuando la cultura de la request es "es".
    /// </summary>
    public async Task<IActionResult> OnPostAsync(
        [FromForm] string? lat,
        [FromForm] string? lng,
        [FromForm] string? city,
        CancellationToken ct)
    {
        if (_auth.CurrentUserId is not int userId)
            return new JsonResult(new { ok = false, error = "login" }) { StatusCode = 401 };

        var result = await _saveLocation.HandleAsync(new SaveLocationCommand(userId, lat, lng, city), ct);
        return result.Outcome switch
        {
            SaveLocationOutcome.InvalidCoordinates => new JsonResult(new { ok = false, error = "coords" }),
            SaveLocationOutcome.UserNotFound => new JsonResult(new { ok = false, error = "user" }) { StatusCode = 404 },
            _ => new JsonResult(new { ok = true, state = result.UsState, city = result.City })
        };
    }
}
