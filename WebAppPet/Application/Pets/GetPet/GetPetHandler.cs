using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Application.Pets.GetPet;

/// <summary>One of the owner's pets (read-only), or null when it belongs to someone else.</summary>
public class GetPetHandler
{
    private readonly AppDbContext _db;

    public GetPetHandler(AppDbContext db) => _db = db;

    public Task<Pet?> HandleAsync(GetPetQuery query, CancellationToken ct = default) =>
        _db.Pets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == query.PetId && p.OwnerId == query.OwnerId, ct);
}
