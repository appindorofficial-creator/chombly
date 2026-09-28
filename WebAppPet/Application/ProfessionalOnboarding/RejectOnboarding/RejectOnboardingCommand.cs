namespace WebAppPet.Application.ProfessionalOnboarding.RejectOnboarding;

/// <param name="Notes">What the applicant must fix; sent to them as the notification.</param>
public sealed record RejectOnboardingCommand(int ApplicationId, int ReviewerUserId, string Notes);
