using Microsoft.EntityFrameworkCore;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Models;

namespace WebAppPet.Application.Behavior.Shared;

public static class BehaviorCaseQueries
{
    /// <summary>The client's case with pet and provider, tracked for changes. Null when it is not theirs.</summary>
    public static Task<BehaviorCase?> OwnedBehaviorCaseAsync(this AppDbContext db, int clientId, int caseId, CancellationToken ct = default) =>
        db.BehaviorCases
            .Include(c => c.Pet)
            .Include(c => c.Provider)!.ThenInclude(p => p!.Category)
            .FirstOrDefaultAsync(c => c.Id == caseId && c.ClientId == clientId, ct);

    /// <summary>Marks the case as updated now and saves all pending changes.</summary>
    public static Task TouchAsync(this AppDbContext db, BehaviorCase behaviorCase, CancellationToken ct = default)
    {
        behaviorCase.UpdatedAt = DateTime.UtcNow;
        return db.SaveChangesAsync(ct);
    }

    /// <summary>Behavior sessions are for dogs only.</summary>
    public static Task<List<Pet>> ClientDogsAsync(this AppDbContext db, int clientId, CancellationToken ct = default) =>
        db.Pets.AsNoTracking()
            .Where(p => p.OwnerId == clientId && p.Species == PetSpecies.Dog)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

    /// <summary>The case's dogs in the order they were selected, skipping any that are no longer the client's dogs.</summary>
    public static async Task<List<Pet>> SelectedDogsAsync(this AppDbContext db, int clientId, BehaviorCase behaviorCase, CancellationToken ct = default)
    {
        var ids = BehaviorCasePets.SelectedIds(behaviorCase);
        if (ids.Count == 0)
            return [];

        var pets = await db.Pets.AsNoTracking()
            .Where(p => p.OwnerId == clientId && ids.Contains(p.Id) && p.Species == PetSpecies.Dog)
            .ToListAsync(ct);
        return ids
            .Select(id => pets.FirstOrDefault(p => p.Id == id))
            .OfType<Pet>()
            .ToList();
    }
}
