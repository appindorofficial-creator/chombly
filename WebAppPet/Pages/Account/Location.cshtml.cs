using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Services;

namespace WebAppPet.Pages.Account;

[IgnoreAntiforgeryToken]
public class LocationModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public LocationModel(AppDbContext db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    /// <summary>
    /// Guarda lat/lng del navegador. Usa string + InvariantCulture para evitar 400
    /// por model binding cuando la cultura de la request es "es" (coma decimal).
    /// </summary>
    public async Task<IActionResult> OnPostAsync([FromForm] string? lat, [FromForm] string? lng)
    {
        if (_auth.CurrentUserId is not int userId)
            return new JsonResult(new { ok = false, error = "login" }) { StatusCode = 401 };

        if (!TryParseCoord(lat, out var latitude) || !TryParseCoord(lng, out var longitude) ||
            latitude is < -90 or > 90 || longitude is < -180 or > 180)
            return new JsonResult(new { ok = false, error = "coords" });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return new JsonResult(new { ok = false, error = "user" }) { StatusCode = 404 };

        user.Latitude = latitude;
        user.Longitude = longitude;
        user.LocationUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new JsonResult(new { ok = true });
    }

    private static bool TryParseCoord(string? value, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        // JS siempre manda punto; aceptamos también coma por si la cultura del cliente interfiere.
        value = value.Trim().Replace(',', '.');
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
