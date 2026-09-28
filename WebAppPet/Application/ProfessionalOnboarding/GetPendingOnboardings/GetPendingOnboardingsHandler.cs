using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Application.ProfessionalOnboarding.GetPendingOnboardings;

/// <summary>Applications waiting for an admin, oldest submission first.</summary>
public class GetPendingOnboardingsHandler
{
    private readonly AppDbContext _db;

    public GetPendingOnboardingsHandler(AppDbContext db) => _db = db;

    public Task<List<ProfessionalOnboardingApplication>> HandleAsync(CancellationToken ct = default) =>
        _db.ProfessionalOnboardingApplications.AsNoTracking()
            .Include(a => a.User)
            .Where(a => a.Status == ProfessionalOnboardingStatus.Submitted
                        || a.Status == ProfessionalOnboardingStatus.UnderReview)
            .OrderBy(a => a.SubmittedUtc)
            .ToListAsync(ct);
}
