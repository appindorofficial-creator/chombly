using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Services;
using WebAppPet.Ui;

namespace WebAppPet.Pages.Account;

public class EditProfileModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly IStringLocalizer<SharedResource> _L;

    public EditProfileModel(AppDbContext db, AuthService auth, IStringLocalizer<SharedResource> L)
    {
        _db = db;
        _auth = auth;
        _L = L;
    }

    [BindProperty, Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [BindProperty, Required, EmailAddress, MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [BindProperty, MaxLength(10)]
    public string? Phone { get; set; }

    [BindProperty, MaxLength(120)]
    public string City { get; set; } = string.Empty;

    [BindProperty]
    public string? Latitude { get; set; }

    [BindProperty]
    public string? Longitude { get; set; }

    [BindProperty]
    public string? NewPassword { get; set; }

    [BindProperty]
    public string? ConfirmPassword { get; set; }

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int id)
            return RedirectToPage("/Account/Login", new { returnUrl = "/Account/EditProfile" });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
            return RedirectToPage("/Account/Login");

        FullName = user.FullName;
        Email = user.Email;
        Phone = user.Phone;
        City = user.City;
        Latitude = user.Latitude?.ToString("0.######", CultureInfo.InvariantCulture);
        Longitude = user.Longitude?.ToString("0.######", CultureInfo.InvariantCulture);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is not int id)
            return RedirectToPage("/Account/Login");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
            return RedirectToPage("/Account/Login");

        FullName = (FullName ?? "").Trim();
        Email = (Email ?? "").Trim().ToLowerInvariant();
        Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim();
        City = (City ?? "").Trim();

        if (!PhoneValidator.TryNormalize(Phone, out var phoneNorm))
        {
            ErrorMessage = _L["Phone_Invalid"].Value;
            return Page();
        }
        Phone = phoneNorm;

        if (string.IsNullOrWhiteSpace(FullName))
        {
            ErrorMessage = _L["Profile_Edit_NameRequired"].Value;
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Email) || !new EmailAddressAttribute().IsValid(Email))
        {
            ErrorMessage = _L["Profile_Edit_EmailInvalid"].Value;
            return Page();
        }

        var emailTaken = await _db.Users.AnyAsync(u => u.Email == Email && u.Id != id);
        if (emailTaken)
        {
            ErrorMessage = _L["Profile_Edit_EmailTaken"].Value;
            return Page();
        }

        if (!TryResolveCityCoordinates(user, out var lat, out var lng, out var cityError))
        {
            ErrorMessage = cityError;
            return Page();
        }

        var changingPassword = !string.IsNullOrWhiteSpace(NewPassword) || !string.IsNullOrWhiteSpace(ConfirmPassword);
        if (changingPassword)
        {
            if (string.IsNullOrWhiteSpace(NewPassword) || !PasswordPolicy.IsValid(NewPassword))
            {
                ErrorMessage = _L["Profile_Edit_PasswordShort"].Value;
                return Page();
            }

            if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
            {
                ErrorMessage = _L["Profile_Edit_PasswordMismatch"].Value;
                return Page();
            }

            user.PasswordHash = PasswordHasher.Hash(NewPassword);
        }

        user.FullName = FullName;
        user.Email = Email;
        user.Phone = Phone;
        user.City = City;
        if (lat is not null && lng is not null)
        {
            user.Latitude = lat;
            user.Longitude = lng;
            user.LocationUpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        await _auth.SignInAsync(user);

        SuccessMessage = _L["Profile_Edit_Saved"].Value;
        AppFlash.Toast(this, "✓ " + _L["Feedback_Saved"].Value);
        NewPassword = null;
        ConfirmPassword = null;
        Latitude = lat?.ToString("0.######", CultureInfo.InvariantCulture);
        Longitude = lng?.ToString("0.######", CultureInfo.InvariantCulture);
        return Page();
    }

    private bool TryResolveCityCoordinates(Models.AppUser user, out double? lat, out double? lng, out string? error)
    {
        lat = null;
        lng = null;
        error = null;

        if (string.IsNullOrWhiteSpace(City))
            return true;

        var parsedLat = TryParseCoord(Latitude, out var postedLat);
        var parsedLng = TryParseCoord(Longitude, out var postedLng);
        var hasPostedCoords = parsedLat && parsedLng && IsValidCoordPair(postedLat, postedLng);
        var cityUnchanged = string.Equals(City, user.City?.Trim(), StringComparison.OrdinalIgnoreCase);
        var hasStoredCoords = user.Latitude is double slat && user.Longitude is double slng
                              && IsValidCoordPair(slat, slng);

        if (hasPostedCoords)
        {
            lat = postedLat;
            lng = postedLng;
            return true;
        }

        // Ciudad sin cambiar y ya había GPS/coords: conservar.
        if (cityUnchanged && hasStoredCoords)
        {
            lat = user.Latitude;
            lng = user.Longitude;
            return true;
        }

        error = _L["Profile_Edit_CityMapsRequired"].Value;
        return false;
    }

    private static bool IsValidCoordPair(double lat, double lng) =>
        lat is >= -90 and <= 90 && lng is >= -180 and <= 180 && !(lat == 0 && lng == 0);

    private static bool TryParseCoord(string? value, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        value = value.Trim().Replace(',', '.');
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
