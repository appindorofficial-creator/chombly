using WebAppPet.Application.Care.Shared;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Care.CancelCare;

/// <summary>
/// Stops renewal: the membership and its benefits last until the current cycle ends.
/// False when there is no active membership.
/// </summary>
public class CancelCareHandler
{
    private readonly AppDbContext _db;
    private readonly ChomblyCareService _care;

    public CancelCareHandler(AppDbContext db, ChomblyCareService care)
    {
        _db = db;
        _care = care;
    }

    public async Task<bool> HandleAsync(CancelCareCommand command, CancellationToken ct = default)
    {
        var subscription = await _care.GetActiveAsync(command.UserId, ct);
        if (subscription is null)
            return false;

        subscription.CancelAtPeriodEnd = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
