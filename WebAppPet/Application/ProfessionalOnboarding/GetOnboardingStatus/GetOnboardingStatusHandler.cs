using WebAppPet.Application.ProfessionalOnboarding.Shared;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.ProfessionalOnboarding.GetOnboardingStatus;

/// <summary>The business's latest professional application and where it stands.</summary>
public class GetOnboardingStatusHandler
{
    private readonly AppDbContext _db;

    public GetOnboardingStatusHandler(AppDbContext db) => _db = db;

    public async Task<OnboardingStatusView> HandleAsync(GetOnboardingStatusQuery query, CancellationToken ct = default)
    {
        if (!await _db.HasBusinessProfileAsync(query.UserId, ct))
            return new OnboardingStatusView(false, null);

        return new OnboardingStatusView(true, await _db.LatestOnboardingAsync(query.UserId, ct));
    }
}
