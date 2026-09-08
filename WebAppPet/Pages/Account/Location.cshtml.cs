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

    public async Task<IActionResult> OnPostAsync([FromForm] double lat, [FromForm] double lng)
    {
        if (_auth.CurrentUserId is not int userId)
            return new JsonResult(new { ok = false, error = "login" }) { StatusCode = 401 };

        if (lat is < -90 or > 90 || lng is < -180 or > 180)
            return new JsonResult(new { ok = false, error = "coords" }) { StatusCode = 400 };

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return new JsonResult(new { ok = false, error = "user" }) { StatusCode = 404 };

        user.Latitude = lat;
        user.Longitude = lng;
        user.LocationUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new JsonResult(new { ok = true });
    }
}
