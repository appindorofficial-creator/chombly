using WebAppPet.Models;

namespace WebAppPet.Application.ProfessionalOnboarding.GetOnboardingStatus;

public sealed record GetOnboardingStatusQuery(int UserId);

/// <param name="Application">The latest application; null when the business never applied.</param>
public sealed record OnboardingStatusView(bool HasBusinessProfile, ProfessionalOnboardingApplication? Application);
