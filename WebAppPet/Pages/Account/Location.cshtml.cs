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
    /// Guarda lat/lng (y ciudad opcional) del navegador. Usa string + InvariantCulture
    /// para evitar 400 por model binding cuando la cultura de la request es "es".
    /// </summary>
    public async Task<IActionResult> OnPostAsync(
        [FromForm] string? lat,
        [FromForm] string? lng,
        [FromForm] string? city)
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

        if (!string.IsNullOrWhiteSpace(city))
        {
            var trimmed = city.Trim();
            if (trimmed.Length > 120) trimmed = trimmed[..120];
            user.City = trimmed;
        }

        await _db.SaveChangesAsync();

        var state = GeoHelper.ResolveUsState(user.City, latitude, longitude);
        return new JsonResult(new { ok = true, state, city = user.City });
    }

    private static bool TryParseCoord(string? value, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        value = value.Trim().Replace(',', '.');
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
