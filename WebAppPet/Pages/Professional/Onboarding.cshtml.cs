using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Professional;

public class OnboardingModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly ProfessionalOnboardingService _onboarding;

    public OnboardingModel(AppDbContext db, AuthService auth, ProfessionalOnboardingService onboarding)
    {
        _db = db;
        _auth = auth;
        _onboarding = onboarding;
    }

    public ProfessionalOnboardingApplication? Latest { get; set; }
    public bool HasBusinessProfile { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = "/Professional/Onboarding" });

        if (!_auth.IsGroomer && !_auth.IsAdmin)
            return RedirectToPage("/Account/RegisterBusiness");

        HasBusinessProfile = await _db.Groomers.AnyAsync(g => g.UserId == _auth.CurrentUserId);
        if (!HasBusinessProfile)
            return RedirectToPage("/Account/RegisterBusiness");

        Latest = await _onboarding.GetLatestAsync(_auth.CurrentUserId.Value);
        if (Latest is { Status: ProfessionalOnboardingStatus.Submitted or ProfessionalOnboardingStatus.UnderReview or ProfessionalOnboardingStatus.Approved })
            return RedirectToPage("/Professional/Onboarding/Status");

        return Page();
    }
}
