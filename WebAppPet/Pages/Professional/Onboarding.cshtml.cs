using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
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
    public BusinessMarket Market { get; set; } = BusinessMarket.Unknown;
    public string? MarketLabel { get; set; }
    public List<TrackCard> Tracks { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = "/Professional/Onboarding" });

        if (!_auth.IsGroomer && !_auth.IsAdmin)
            return RedirectToPage("/Account/RegisterBusiness");

        var profile = await _db.Groomers.AsNoTracking()
            .FirstOrDefaultAsync(g => g.UserId == _auth.CurrentUserId);
        HasBusinessProfile = profile is not null;
        if (!HasBusinessProfile)
            return RedirectToPage("/Account/RegisterBusiness");

        Latest = await _onboarding.GetLatestAsync(_auth.CurrentUserId.Value);
        if (Latest is { Status: ProfessionalOnboardingStatus.Submitted or ProfessionalOnboardingStatus.UnderReview or ProfessionalOnboardingStatus.Approved })
            return RedirectToPage("/Professional/Onboarding/Status");

        Market = BusinessMarketResolver.Resolve(profile);
        MarketLabel = Market switch
        {
            BusinessMarket.Colombia => CatalogLocalizer.Loc("Colombia", "Colombia"),
            BusinessMarket.UnitedStates => CatalogLocalizer.Loc("Estados Unidos", "United States"),
            _ => null
        };
        Tracks = BuildTracks(Market);
        return Page();
    }

    private static List<TrackCard> BuildTracks(BusinessMarket market)
    {
        var usLocal = new TrackCard(
            Page: "/Professional/Onboarding/Local",
            Track: null,
            Icon: "🇺🇸",
            TitleEs: "Veterinario EE.UU.",
            TitleEn: "US veterinarian",
            BodyEs: "Licencia estatal, clínica y VCPR.",
            BodyEn: "State license, clinic, and VCPR.",
            Emphasized: market == BusinessMarket.UnitedStates);

        var colombiaLocal = new TrackCard(
            Page: "/Professional/Onboarding/International",
            Track: null,
            Icon: "🇨🇴",
            TitleEs: "Veterinario en Colombia",
            TitleEn: "Veterinarian in Colombia",
            BodyEs: "País, idiomas y expertise. Sin licencia US / VCPR.",
            BodyEn: "Country, languages, and expertise. No US license / VCPR.",
            Emphasized: market == BusinessMarket.Colombia);

        return market switch
        {
            BusinessMarket.UnitedStates => new List<TrackCard> { usLocal, colombiaLocal },
            _ => new List<TrackCard> { colombiaLocal, usLocal }
        };
    }

    public record TrackCard(
        string Page,
        string? Track,
        string Icon,
        string TitleEs,
        string TitleEn,
        string BodyEs,
        string BodyEn,
        bool Emphasized);
}
