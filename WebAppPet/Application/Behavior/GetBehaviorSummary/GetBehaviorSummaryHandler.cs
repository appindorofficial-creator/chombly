using WebAppPet.Application.Behavior.Shared;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Application.Behavior.GetBehaviorSummary;

/// <summary>The client's case with its pet and specialist. Null when it is not theirs.</summary>
public class GetBehaviorSummaryHandler
{
    private readonly AppDbContext _db;

    public GetBehaviorSummaryHandler(AppDbContext db) => _db = db;

    public Task<BehaviorCase?> HandleAsync(GetBehaviorSummaryQuery query, CancellationToken ct = default) =>
        _db.OwnedBehaviorCaseAsync(query.ClientId, query.CaseId, ct);
}
