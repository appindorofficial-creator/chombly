using WebAppPet.Domain;


namespace WebAppPet.Application.ProfessionalOnboarding.ApproveOnboarding;

public sealed record ApproveOnboardingCommand(int ApplicationId, int ReviewerUserId, string? Notes = null);

public enum ApproveOnboardingOutcome
{
    NotFound,

    /// <summary>The application is not waiting for review (draft, approved or rejected); nothing changed.</summary>
    NotPending,

    /// <summary>The applicant has no business profile to upgrade.</summary>
    NoBusinessProfile,
    Approved
}

public sealed record ApproveOnboardingResult(ApproveOnboardingOutcome Outcome, ProfessionalOnboardingApplication? Application);
