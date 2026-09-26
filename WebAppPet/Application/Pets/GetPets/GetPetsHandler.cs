using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Pets.GetPets;

/// <summary>
/// The owner's pets by name. Photos uploaded to /uploads that were lost in old deploys
/// are reset to the species placeholder.
/// </summary>
public class GetPetsHandler
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public GetPetsHandler(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<List<Pet>> HandleAsync(GetPetsQuery query, CancellationToken ct = default)
    {
        var pets = await _db.Pets.Where(p => p.OwnerId == query.OwnerId).OrderBy(p => p.Name).ToListAsync(ct);

        var healed = false;
        foreach (var pet in pets)
        {
            if (!PetPhotoStorage.IsMissingLocalUpload(pet.PhotoUrl, _env))
                continue;
            pet.PhotoUrl = PetSpecies.DefaultPhoto(pet.Species);
            healed = true;
        }
        if (healed)
            await _db.SaveChangesAsync(ct);

        return pets;
    }
}
