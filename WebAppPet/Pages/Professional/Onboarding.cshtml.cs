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
    /// <summary>False for grooming/hotel/walkers/etc. — vet/behavior tracks only.</summary>
    public bool ProfessionalTracksApply { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = "/Professional/Onboarding" });

        if (!_auth.IsGroomer && !_auth.IsAdmin)
            return RedirectToPage("/Account/RegisterBusiness");

        var profile = await _db.Groomers.AsNoTracking()
            .Include(g => g.Category)
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

        var offeredSlugs = await ResolveOfferedSlugsAsync(profile!);
        ProfessionalTracksApply = NeedsProfessionalTrack(profile!, offeredSlugs);
        Tracks = ProfessionalTracksApply
            ? BuildTracks(Market, profile!, offeredSlugs)
            : [];
        return Page();
    }

    private async Task<HashSet<string>> ResolveOfferedSlugsAsync(GroomerProfile profile)
    {
        var ids = profile.GetOfferedCategoryIds();
        if (ids.Count == 0) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var slugs = await _db.Categories.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => c.Slug)
            .ToListAsync();
        return slugs.Where(s => !string.IsNullOrWhiteSpace(s))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool NeedsProfessionalTrack(GroomerProfile profile, HashSet<string> offeredSlugs)
    {
        if (profile.VetProviderKind != VetProviderKind.None)
            return true;
        return offeredSlugs.Contains("vet") || offeredSlugs.Contains("trainers");
    }

    /// <summary>Vet/behavior tracks gated by market and what the business actually offers.</summary>
    private static List<TrackCard> BuildTracks(BusinessMarket market, GroomerProfile profile, HashSet<string> offeredSlugs)
    {
        var tracks = new List<TrackCard>();
        var isVet = profile.VetProviderKind is VetProviderKind.LocalVet or VetProviderKind.InternationalAdvisor
            || offeredSlugs.Contains("vet");
        var isBehavior = profile.VetProviderKind == VetProviderKind.BehaviorSpecialist
            || offeredSlugs.Contains("trainers");

        if (isVet)
        {
            if (market == BusinessMarket.UnitedStates)
            {
                tracks.Add(new TrackCard(
                    Page: "/Professional/Onboarding/Local",
                    Track: null,
                    Icon: "🇺🇸",
                    TitleEs: "Veterinario EE.UU.",
                    TitleEn: "US veterinarian",
                    BodyEs: "Licencia estatal, clínica y VCPR.",
                    BodyEn: "State license, clinic, and VCPR.",
                    Emphasized: true));
            }
            else
            {
                tracks.Add(new TrackCard(
                    Page: "/Professional/Onboarding/International",
                    Track: null,
                    Icon: "🇨🇴",
                    TitleEs: "Veterinario en Colombia",
                    TitleEn: "Veterinarian in Colombia",
                    BodyEs: "País, idiomas y expertise. Sin licencia US / VCPR.",
                    BodyEn: "Country, languages, and expertise. No US license / VCPR.",
                    Emphasized: true));
            }
        }

        if (isBehavior)
        {
            tracks.Add(new TrackCard(
                Page: "/Professional/Onboarding/Local",
                Track: "behavior",
                Icon: "🧠",
                TitleEs: "Conducta / entrenamiento",
                TitleEn: "Behavior / training",
                BodyEs: "Credenciales y enfoque educativo (no diagnóstico médico).",
                BodyEn: "Credentials and educational focus (not a medical diagnosis).",
                Emphasized: !isVet));
        }

        return tracks;
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
