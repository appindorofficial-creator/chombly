using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Professional.Onboarding;

public class StatusModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly ProfessionalOnboardingService _onboarding;

    public StatusModel(AppDbContext db, AuthService auth, ProfessionalOnboardingService onboarding)
    {
        _db = db;
        _auth = auth;
        _onboarding = onboarding;
    }

    public ProfessionalOnboardingApplication? Application { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login");
        if (!_auth.IsGroomer && !_auth.IsAdmin)
            return RedirectToPage("/Account/RegisterBusiness");
        if (!await _db.Groomers.AnyAsync(g => g.UserId == _auth.CurrentUserId))
            return RedirectToPage("/Account/RegisterBusiness");

        Application = await _onboarding.GetLatestAsync(_auth.CurrentUserId.Value);
        return Page();
    }
}
