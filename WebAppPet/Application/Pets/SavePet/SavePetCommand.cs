using WebAppPet.Models;

namespace WebAppPet.Application.Pets.SavePet;

/// <param name="PetId">Null to add a new pet; otherwise the owner's pet to edit.</param>
/// <param name="CustomType">Free-text type when <paramref name="Species"/> is "other"; stored as the breed.</param>
/// <param name="CustomBreed">Free-text breed when <paramref name="Breed"/> is the "other" option.</param>
/// <param name="PhotoUrl">Photo URL kept from the form when no file is uploaded.</param>
public sealed record SavePetCommand(
    int OwnerId,
    int? PetId,
    string? Name,
    string? Species,
    string? CustomType,
    string? Breed,
    string? CustomBreed,
    int? AgeYears,
    PetSize Size,
    IReadOnlyList<string>? Temperaments,
    string? PhotoUrl,
    PetPhotoUpload? Photo,
    bool IsSenior,
    bool IsAnxious,
    bool HasSpecialNeeds,
    string? Notes);

/// <summary>An uploaded photo. It is only saved once the rest of the form is valid.</summary>
/// <param name="ValidationError">Localization key when the file is not an accepted image.</param>
public sealed record PetPhotoUpload(string? ValidationError, Func<CancellationToken, Task<string>> SaveAsync);

public enum PetField
{
    Name,
    Species,
    CustomType,
    AgeYears,
    Temperaments,
    Photo
}

public sealed record PetFieldError(PetField Field, string MessageKey);

public enum SavePetStatus
{
    Created,
    Updated,
    NotFound,
    Invalid
}

public sealed record SavePetResult(SavePetStatus Status, IReadOnlyList<PetFieldError> Errors, Pet? Pet)
{
    public bool Success => Status is SavePetStatus.Created or SavePetStatus.Updated;

    public static SavePetResult Invalid(IReadOnlyList<PetFieldError> errors) => new(SavePetStatus.Invalid, errors, null);
}
