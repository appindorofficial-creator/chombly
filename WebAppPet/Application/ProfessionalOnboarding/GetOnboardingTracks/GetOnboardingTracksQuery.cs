using WebAppPet.Domain;
using WebAppPet.Domain.Markets;

namespace WebAppPet.Application.ProfessionalOnboarding.GetOnboardingTracks;

public sealed record GetOnboardingTracksQuery(int UserId);

public enum OnboardingTracksRedirect
{
    RegisterBusiness,

    /// <summary>An application is already under review or approved.</summary>
    Status
}

/// <param name="ProfessionalTracksApply">False for grooming, hotels, walkers and the like: only vets and behavior specialists verify.</param>
public sealed record OnboardingTracksView(
    OnboardingTracksRedirect? Redirect,
    ProfessionalOnboardingApplication? Latest,
    BusinessMarket Market,
    bool ProfessionalTracksApply,
    List<OnboardingTrackCard> Tracks)
{
    public static OnboardingTracksView RedirectTo(OnboardingTracksRedirect redirect) =>
        new(redirect, null, BusinessMarket.Unknown, false, []);
}

/// <param name="Page">Form that starts the track.</param>
/// <param name="Track">Route value for the form ("behavior"), or null.</param>
/// <param name="Emphasized">The track that best fits the business.</param>
public sealed record OnboardingTrackCard(
    string Page,
    string? Track,
    string Icon,
    string TitleEs,
    string TitleEn,
    string BodyEs,
    string BodyEn,
    bool Emphasized);
