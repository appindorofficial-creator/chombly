using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;

namespace WebAppPet.Application.Pets.SavePet;

/// <summary>
/// Adds or edits a pet. All field errors are returned together; an owner cannot have two pets
/// with the same species, name and breed.
/// </summary>
public class SavePetHandler
{
    private const int MaxAge = 40;

    private readonly AppDbContext _db;
    private readonly IStringLocalizer<SharedResource> _L;

    public SavePetHandler(AppDbContext db, IStringLocalizer<SharedResource> L)
    {
        _db = db;
        _L = L;
    }

    public async Task<SavePetResult> HandleAsync(SavePetCommand command, CancellationToken ct = default)
    {
        Pet? existing = null;
        if (command.PetId is > 0)
        {
            existing = await _db.Pets.FirstOrDefaultAsync(p => p.Id == command.PetId && p.OwnerId == command.OwnerId, ct);
            if (existing is null)
                return new SavePetResult(SavePetStatus.NotFound, [], null);
        }

        var temperaments = NormalizeTemperaments(command.Temperaments);
        var errors = Validate(command, temperaments);
        if (errors.Count > 0)
            return SavePetResult.Invalid(errors);

        var species = command.Species!.Trim();
        var name = command.Name!.Trim();
        var breed = ResolveBreed(command, species);

        var duplicate = await _db.Pets.AsNoTracking().AnyAsync(p =>
            p.OwnerId == command.OwnerId
            && (existing == null || p.Id != existing.Id)
            && p.Species == species
            && p.Name.ToLower() == name.ToLower()
            && p.Breed.ToLower() == breed.ToLower(), ct);
        if (duplicate)
            return SavePetResult.Invalid([new PetFieldError(PetField.Name, "Pets_Duplicate")]);

        var photoUrl = existing?.PhotoUrl;
        if (command.Photo is not null)
            photoUrl = await command.Photo.SaveAsync(ct);
        else if (!string.IsNullOrWhiteSpace(command.PhotoUrl))
            photoUrl = command.PhotoUrl.Trim();

        var pet = existing ?? new Pet { OwnerId = command.OwnerId };
        pet.Name = name;
        pet.Species = species;
        pet.Breed = breed;
        pet.AgeYears = command.AgeYears!.Value;
        pet.Size = command.Size;
        pet.Temperament = string.Join(", ", temperaments);
        pet.PhotoUrl = photoUrl ?? PetSpecies.DefaultPhoto(species);
        pet.IsSenior = command.IsSenior;
        pet.IsAnxious = command.IsAnxious;
        pet.HasSpecialNeeds = command.HasSpecialNeeds;
        pet.Notes = command.Notes;

        if (existing is null)
            _db.Pets.Add(pet);
        await _db.SaveChangesAsync(ct);

        return new SavePetResult(existing is null ? SavePetStatus.Created : SavePetStatus.Updated, [], pet);
    }

    private static List<PetFieldError> Validate(SavePetCommand command, List<string> temperaments)
    {
        var errors = new List<PetFieldError>();

        if (string.IsNullOrWhiteSpace(command.Name))
            errors.Add(new(PetField.Name, "Pets_NameRequired"));

        if (string.IsNullOrWhiteSpace(command.Species))
            errors.Add(new(PetField.Species, "Pets_SpeciesRequired"));
        else if (!PetSpecies.IsKnown(command.Species))
            errors.Add(new(PetField.Species, "Pets_SpeciesInvalid"));
        else if (command.Species == PetSpecies.Other && string.IsNullOrWhiteSpace(command.CustomType))
            errors.Add(new(PetField.CustomType, "Pets_OtherTypeRequired"));

        if (command.AgeYears is null)
            errors.Add(new(PetField.AgeYears, "Pets_AgeRequired"));
        else if (command.AgeYears is < 0 or > MaxAge)
            errors.Add(new(PetField.AgeYears, "Pets_AgeRange"));

        if (temperaments.Count == 0)
            errors.Add(new(PetField.Temperaments, "Pets_TemperamentRequired"));

        if (command.Photo?.ValidationError is { } photoError)
            errors.Add(new(PetField.Photo, photoError));

        return errors;
    }

    /// <summary>Known temperaments only, deduplicated and written as in the catalog.</summary>
    private static List<string> NormalizeTemperaments(IReadOnlyList<string>? selected) =>
        (selected ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Where(t => PetCatalog.Temperaments.Any(o => o.Value.Equals(t, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(t => PetCatalog.Temperaments.First(o => o.Value.Equals(t, StringComparison.OrdinalIgnoreCase)).Value)
            .ToList();

    private string ResolveBreed(SavePetCommand command, string species)
    {
        var breedDefault = _L["Pets_BreedDefault"].Value;
        if (species == PetSpecies.Other)
            return command.CustomType!.Trim();

        if (string.Equals(command.Breed, PetCatalog.OtherBreed, StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(command.CustomBreed) ? breedDefault : command.CustomBreed.Trim();

        return string.IsNullOrWhiteSpace(command.Breed) ? breedDefault : command.Breed.Trim();
    }
}
