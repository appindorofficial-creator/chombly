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
            .Include(c => c.Provider)!.ThenInclude(p => p!.Category)
            .FirstOrDefaultAsync(c => c.Id == id && c.ClientId == uid.Value, ct);
    }

    public async Task<BehaviorCase> StartAsync(int? petId = null, CancellationToken ct = default)
    {
        if (_auth.CurrentUserId is not int uid)
            throw new InvalidOperationException("Login required.");

        int? ownedPetId = null;
        if (petId is int pid && pid > 0)
        {
            var owns = await _db.Pets.AsNoTracking()
                .AnyAsync(p => p.Id == pid && p.OwnerId == uid, ct);
            if (owns) ownedPetId = pid;
        }

        var c = new BehaviorCase
        {
            ClientId = uid,
            PetId = ownedPetId,
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

    public static List<int> GetSelectedPetIds(BehaviorCase c)
    {
        var ids = new List<int>();
        if (c.PetId is int primary && primary > 0)
            ids.Add(primary);

        if (!string.IsNullOrWhiteSpace(c.ExtraPetIds))
        {
            foreach (var part in c.ExtraPetIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out var id) && id > 0 && !ids.Contains(id))
                    ids.Add(id);
            }
        }

        return ids;
    }

    public static void SetSelectedPetIds(BehaviorCase c, IEnumerable<int> petIds)
    {
        var ids = petIds.Where(id => id > 0).Distinct().ToList();
        c.PetId = ids.Count > 0 ? ids[0] : null;
        c.ExtraPetIds = ids.Count > 1
            ? string.Join(",", ids.Skip(1))
            : null;
    }

    /// <summary>Clinical flags that must go to veterinary care first.</summary>
    public static bool HasClinicalRedFlag(bool suddenAggression, bool seizureLike, bool painSuspected, bool selfHarm)
        => suddenAggression || seizureLike || painSuspected || selfHarm;
}
