using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Application.Accounts.Shared;
using WebAppPet.Application.Accounts.UpdateProfile;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Services;
using WebAppPet.Ui;

namespace WebAppPet.Pages.Account;

public class EditProfileModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UpdateProfileHandler _updateProfile;
    private readonly AuthService _auth;
    private readonly IStringLocalizer<SharedResource> _L;

    public EditProfileModel(
        AppDbContext db,
        UpdateProfileHandler updateProfile,
        AuthService auth,
        IStringLocalizer<SharedResource> L)
    {
        _db = db;
        _updateProfile = updateProfile;
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

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
            return RedirectToPage("/Account/Login");

        FullName = user.FullName;
        Email = user.Email;
        Phone = user.Phone;
        City = user.City;
        Latitude = Coordinates.Format(user.Latitude);
        Longitude = Coordinates.Format(user.Longitude);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is not int id)
            return RedirectToPage("/Account/Login");

        var result = await _updateProfile.HandleAsync(new UpdateProfileCommand(
            id, FullName, Email, Phone, City, Latitude, Longitude, NewPassword, ConfirmPassword));

        if (result.Error == UpdateProfileError.NotFound)
            return RedirectToPage("/Account/Login");

        if (result.User is not { } user)
        {
            ErrorMessage = result.Error switch
            {
                UpdateProfileError.PhoneInvalid => _L["Phone_Invalid"].Value,
                UpdateProfileError.NameRequired => _L["Profile_Edit_NameRequired"].Value,
                UpdateProfileError.EmailInvalid => _L["Profile_Edit_EmailInvalid"].Value,
                UpdateProfileError.EmailTaken => _L["Profile_Edit_EmailTaken"].Value,
                UpdateProfileError.CityLocationRequired => _L["Profile_Edit_CityMapsRequired"].Value,
                UpdateProfileError.PasswordWeak => _L["Profile_Edit_PasswordShort"].Value,
                _ => _L["Profile_Edit_PasswordMismatch"].Value
            };
            return Page();
        }

        await _auth.SignInAsync(user);

        SuccessMessage = _L["Profile_Edit_Saved"].Value;
        AppFlash.Toast(this, "✓ " + _L["Feedback_Saved"].Value);
        FullName = user.FullName;
        Email = user.Email;
        Phone = user.Phone;
        City = user.City;
        NewPassword = null;
        ConfirmPassword = null;
        Latitude = Coordinates.Format(result.Latitude);
        Longitude = Coordinates.Format(result.Longitude);
        return Page();
    }
}
