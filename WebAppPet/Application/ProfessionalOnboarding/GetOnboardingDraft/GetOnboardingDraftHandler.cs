using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.ProfessionalOnboarding.Shared;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.ProfessionalOnboarding.GetOnboardingDraft;

/// <summary>What the onboarding forms start from: the editable application, if any, and the business's market.</summary>
public class GetOnboardingDraftHandler
{
    private readonly AppDbContext _db;

    public GetOnboardingDraftHandler(AppDbContext db) => _db = db;

    public async Task<OnboardingDraftView> HandleAsync(GetOnboardingDraftQuery query, CancellationToken ct = default)
    {
        var profile = await _db.Groomers.AsNoTracking().FirstOrDefaultAsync(g => g.UserId == query.UserId, ct);
        if (profile is null)
            return new OnboardingDraftView(false, BusinessMarket.Unknown, null);

        var latest = await _db.LatestOnboardingAsync(query.UserId, ct);
        return new OnboardingDraftView(true, BusinessMarketResolver.Resolve(profile), latest?.IsEditable() == true ? latest : null);
    }
}
