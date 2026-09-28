using WebAppPet.Application.Behavior.Shared;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Behavior.GetBehaviorIntake;

/// <summary>The intake form for the client's case. Null when the case is not theirs.</summary>
public class GetBehaviorIntakeHandler
{
    private readonly AppDbContext _db;

    public GetBehaviorIntakeHandler(AppDbContext db) => _db = db;

    public async Task<BehaviorIntakeForm?> HandleAsync(GetBehaviorIntakeQuery query, CancellationToken ct = default)
    {
        var behaviorCase = await _db.OwnedBehaviorCaseAsync(query.ClientId, query.CaseId, ct);
        if (behaviorCase is null)
            return null;

        return new BehaviorIntakeForm(behaviorCase, await _db.ClientDogsAsync(query.ClientId, ct));
    }
}
