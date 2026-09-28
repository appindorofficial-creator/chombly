using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.ProfessionalOnboarding.Shared;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.ProfessionalOnboarding.GetOnboardingTracks;

/// <summary>
/// The verification tracks the business can start, based on its market and on what it offers:
/// vets get the US or Colombia vet track, trainers the behavior track.
/// </summary>
public class GetOnboardingTracksHandler
{
    private readonly AppDbContext _db;

    public GetOnboardingTracksHandler(AppDbContext db) => _db = db;

    public async Task<OnboardingTracksView> HandleAsync(GetOnboardingTracksQuery query, CancellationToken ct = default)
    {
        var profile = await _db.Groomers.AsNoTracking()
            .Include(g => g.Category)
            .FirstOrDefaultAsync(g => g.UserId == query.UserId, ct);
        if (profile is null)
            return OnboardingTracksView.RedirectTo(OnboardingTracksRedirect.RegisterBusiness);

        var latest = await _db.LatestOnboardingAsync(query.UserId, ct);
        if (latest is not null && (latest.IsPendingReview() || latest.Status == ProfessionalOnboardingStatus.Approved))
            return OnboardingTracksView.RedirectTo(OnboardingTracksRedirect.Status);

        var market = BusinessMarketResolver.Resolve(profile);
        var offeredSlugs = await OfferedSlugsAsync(profile, ct);
        var isVet = profile.VetProviderKind is VetProviderKind.LocalVet or VetProviderKind.InternationalAdvisor
            || offeredSlugs.Contains("vet");
        var isBehavior = profile.VetProviderKind == VetProviderKind.BehaviorSpecialist
            || offeredSlugs.Contains("trainers");
        var applies = profile.VetProviderKind != VetProviderKind.None || isVet || isBehavior;

        return new OnboardingTracksView(null, latest, market, applies, applies ? Tracks(market, isVet, isBehavior) : []);
    }

    private async Task<HashSet<string>> OfferedSlugsAsync(GroomerProfile profile, CancellationToken ct)
    {
        var ids = profile.GetOfferedCategoryIds();
        if (ids.Count == 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var slugs = await _db.Categories.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => c.Slug)
            .ToListAsync(ct);
        return slugs.Where(s => !string.IsNullOrWhiteSpace(s)).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static List<OnboardingTrackCard> Tracks(BusinessMarket market, bool isVet, bool isBehavior)
    {
        var tracks = new List<OnboardingTrackCard>();
        if (isVet)
        {
            tracks.Add(market == BusinessMarket.UnitedStates
                ? new OnboardingTrackCard(
                    Page: "/Professional/Onboarding/Local",
                    Track: null,
                    Icon: "🇺🇸",
                    TitleEs: "Veterinario EE.UU.",
                    TitleEn: "US veterinarian",
                    BodyEs: "Licencia estatal, clínica y VCPR.",
                    BodyEn: "State license, clinic, and VCPR.",
                    Emphasized: true)
                : new OnboardingTrackCard(
                    Page: "/Professional/Onboarding/International",
                    Track: null,
                    Icon: "🇨🇴",
                    TitleEs: "Veterinario en Colombia",
                    TitleEn: "Veterinarian in Colombia",
                    BodyEs: "País, idiomas y expertise. Sin licencia US / VCPR.",
                    BodyEn: "Country, languages, and expertise. No US license / VCPR.",
                    Emphasized: true));
        }

        if (isBehavior)
        {
            tracks.Add(new OnboardingTrackCard(
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
}
