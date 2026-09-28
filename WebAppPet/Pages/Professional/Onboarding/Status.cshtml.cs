using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.ProfessionalOnboarding.GetOnboardingStatus;
using WebAppPet.Application.ProfessionalOnboarding.Shared;
using WebAppPet.Infrastructure.Identity;
using WebAppPet.Models;

namespace WebAppPet.Pages.Professional.Onboarding;

public class StatusModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetOnboardingStatusHandler _getStatus;

    public StatusModel(AuthService auth, GetOnboardingStatusHandler getStatus)
    {
        _auth = auth;
        _getStatus = getStatus;
    }

    public ProfessionalOnboardingApplication? Application { get; set; }

    /// <summary>Continue/edit target for Draft or Rejected applications.</summary>
    public string? ContinueHref { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");
        if (!_auth.IsGroomer && !_auth.IsAdmin)
            return RedirectToPage("/Account/RegisterBusiness");

        var view = await _getStatus.HandleAsync(new GetOnboardingStatusQuery(userId));
        if (!view.HasBusinessProfile)
            return RedirectToPage("/Account/RegisterBusiness");

        Application = view.Application;
        ContinueHref = ResolveContinueHref(Application);
        return Page();
    }

    private static string? ResolveContinueHref(ProfessionalOnboardingApplication? app)
    {
        if (app is null || !app.IsEditable()) return null;

        return app.Track switch
        {
            ProfessionalOnboardingTrack.International => "/Professional/Onboarding/International",
            ProfessionalOnboardingTrack.Behavior => "/Professional/Onboarding/Local?Track=behavior",
            _ => "/Professional/Onboarding/Local"
        };
    }
}
