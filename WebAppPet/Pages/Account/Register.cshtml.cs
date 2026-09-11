using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly IStringLocalizer<SharedResource> _L;

    public RegisterModel(AppDbContext db, AuthService auth, IStringLocalizer<SharedResource> L)
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

    [BindProperty, Required, MaxLength(120)]
    public string City { get; set; } = string.Empty;

    [BindProperty]
    public string? Latitude { get; set; }

    [BindProperty]
    public string? Longitude { get; set; }

    [BindProperty, Required, MinLength(PasswordPolicy.MinLength)]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        FullName = (FullName ?? "").Trim();
        Email = (Email ?? "").Trim().ToLowerInvariant();
        Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim();
        City = (City ?? "").Trim();

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

        if (!PhoneValidator.TryNormalize(Phone, out var phoneNorm))
        {
            ErrorMessage = _L["Phone_Invalid"].Value;
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Password) || !PasswordPolicy.IsValid(Password))
        {
            ErrorMessage = _L["Profile_Edit_PasswordShort"].Value;
            return Page();
        }

        if (!TryParseCoords(out var lat, out var lng))
        {
            ErrorMessage = _L["Register_LocationRequired"].Value;
            return Page();
        }

        if (string.IsNullOrWhiteSpace(City))
            City = _L["Register_LocationOk"].Value;

        if (await _db.Users.AnyAsync(u => u.Email == Email))
        {
            ErrorMessage = _L["Profile_Edit_EmailTaken"].Value;
            return Page();
        }

        var user = new AppUser
        {
            FullName = FullName,
            Email = Email,
            Phone = phoneNorm,
            City = City,
            Latitude = lat,
            Longitude = lng,
            LocationUpdatedAt = DateTime.UtcNow,
            PasswordHash = PasswordHasher.Hash(Password),
            Role = UserRole.Client
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _db.Notifications.Add(new AppNotification
        {
            UserId = user.Id,
            Title = "¡Bienvenido a Chombly!",
            Message = "Tu cuenta fue creada. Agrega tu perro, gato u otra mascota y reserva tu primera cita.",
            Type = "promo"
        });
        await _db.SaveChangesAsync();

        await _auth.SignInAsync(user);
        return RedirectToPage("/Index");
    }

    private bool TryParseCoords(out double lat, out double lng)
    {
        lat = 0;
        lng = 0;
        if (!TryParseCoord(Latitude, out lat) || !TryParseCoord(Longitude, out lng))
            return false;
        if (lat is < -90 or > 90 || lng is < -180 or > 180)
            return false;
        if (lat == 0 && lng == 0)
            return false;
        return true;
    }

    private static bool TryParseCoord(string? value, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        value = value.Trim().Replace(',', '.');
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
