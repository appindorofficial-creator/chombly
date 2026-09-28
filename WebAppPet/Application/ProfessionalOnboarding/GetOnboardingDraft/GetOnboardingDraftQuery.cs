using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.ProfessionalOnboarding.GetOnboardingDraft;

public sealed record GetOnboardingDraftQuery(int UserId);

/// <param name="Market">The business's market; the international form defaults the license country from it.</param>
/// <param name="Draft">The latest application when it can still be edited (draft or rejected), of any track.</param>
public sealed record OnboardingDraftView(bool HasBusinessProfile, BusinessMarket Market, ProfessionalOnboardingApplication? Draft);
