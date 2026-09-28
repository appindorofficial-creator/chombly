using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.ProfessionalOnboarding.GetOnboardingTracks;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Identity;
using WebAppPet.Localization;

namespace WebAppPet.Pages.Professional;

public class OnboardingModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetOnboardingTracksHandler _getTracks;

    public OnboardingModel(AuthService auth, GetOnboardingTracksHandler getTracks)
    {
        _auth = auth;
        _getTracks = getTracks;
    }

    public ProfessionalOnboardingApplication? Latest { get; set; }
    public BusinessMarket Market { get; set; } = BusinessMarket.Unknown;
    public string? MarketLabel { get; set; }
    public List<OnboardingTrackCard> Tracks { get; set; } = new();
    /// <summary>False for grooming/hotel/walkers/etc. — vet/behavior tracks only.</summary>
    public bool ProfessionalTracksApply { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = "/Professional/Onboarding" });

        if (!_auth.IsGroomer && !_auth.IsAdmin)
            return RedirectToPage("/Account/RegisterBusiness");

        var view = await _getTracks.HandleAsync(new GetOnboardingTracksQuery(userId));
        switch (view.Redirect)
        {
            case OnboardingTracksRedirect.RegisterBusiness:
                return RedirectToPage("/Account/RegisterBusiness");
            case OnboardingTracksRedirect.Status:
                return RedirectToPage("/Professional/Onboarding/Status");
        }

        Latest = view.Latest;
        Market = view.Market;
        MarketLabel = Market switch
        {
            BusinessMarket.Colombia => CatalogLocalizer.Loc("Colombia", "Colombia"),
            BusinessMarket.UnitedStates => CatalogLocalizer.Loc("Estados Unidos", "United States"),
            _ => null
        };
        ProfessionalTracksApply = view.ProfessionalTracksApply;
        Tracks = view.Tracks;
        return Page();
    }
}
