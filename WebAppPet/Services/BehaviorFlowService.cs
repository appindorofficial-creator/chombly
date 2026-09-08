using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Services;

public class BehaviorFlowService
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public BehaviorFlowService(AppDbContext db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<BehaviorCase?> GetOwnedAsync(int id, CancellationToken ct = default)
    {
        var uid = _auth.CurrentUserId;
        if (uid is null) return null;
        return await _db.BehaviorCases
            .Include(c => c.Pet)
            .Include(c => c.Provider)
            .FirstOrDefaultAsync(c => c.Id == id && c.ClientId == uid.Value, ct);
    }

    public async Task<BehaviorCase> StartAsync(CancellationToken ct = default)
    {
        if (_auth.CurrentUserId is not int uid)
            throw new InvalidOperationException("Login required.");

        var c = new BehaviorCase
        {
            ClientId = uid,
            Status = BehaviorCaseStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.BehaviorCases.Add(c);
        await _db.SaveChangesAsync(ct);
        return c;
    }

    public async Task TouchAsync(BehaviorCase c, CancellationToken ct = default)
    {
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Clinical flags that must go to veterinary care first.</summary>
    public static bool HasClinicalRedFlag(bool suddenAggression, bool seizureLike, bool painSuspected, bool selfHarm)
        => suddenAggression || seizureLike || painSuspected || selfHarm;
}
