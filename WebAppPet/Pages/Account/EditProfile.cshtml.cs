using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Services;

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

        var changingPassword = !string.IsNullOrWhiteSpace(NewPassword) || !string.IsNullOrWhiteSpace(ConfirmPassword);
        if (changingPassword)
        {
            if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
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
        await _db.SaveChangesAsync();

        // Actualiza cookie (nombre / email en claims)
        await _auth.SignInAsync(user);

        SuccessMessage = _L["Profile_Edit_Saved"].Value;
        NewPassword = null;
        ConfirmPassword = null;
        return Page();
    }
}
