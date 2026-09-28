using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Application.ProfessionalOnboarding.Shared;

public static class OnboardingQueries
{
    /// <summary>The user's most recent application, tracked for changes.</summary>
    public static Task<ProfessionalOnboardingApplication?> LatestOnboardingAsync(this AppDbContext db, int userId, CancellationToken ct = default) =>
        db.ProfessionalOnboardingApplications
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedUtc)
            .FirstOrDefaultAsync(ct);

    /// <summary>Only businesses can apply: the professional tracks upgrade an existing business profile.</summary>
    public static Task<bool> HasBusinessProfileAsync(this AppDbContext db, int userId, CancellationToken ct = default) =>
        db.Groomers.AnyAsync(g => g.UserId == userId, ct);

    public static bool IsEditable(this ProfessionalOnboardingApplication application) =>
        application.Status is ProfessionalOnboardingStatus.Draft or ProfessionalOnboardingStatus.Rejected;

    public static bool IsPendingReview(this ProfessionalOnboardingApplication application) =>
        application.Status is ProfessionalOnboardingStatus.Submitted or ProfessionalOnboardingStatus.UnderReview;
}
