using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Pets.SavePet;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Pets;

public class SavePetHandlerTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly AppUser _owner;

    public SavePetHandlerTests()
    {
        _db = _database.CreateContext();
        _owner = TestData.AddUser(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private static SavePetCommand Command(
        int ownerId,
        int? petId = null,
        string? name = "Toby",
        string? species = PetSpecies.Dog,
        string? customType = null,
        string? breed = "Labrador Retriever",
        string? customBreed = null,
        int? age = 3,
        IReadOnlyList<string>? temperaments = null,
        string? photoUrl = null,
        PetPhotoUpload? photo = null) =>
        new(ownerId, petId, name, species, customType, breed, customBreed, age, PetSize.Large,
            temperaments ?? ["Amigable"], photoUrl, photo, IsSenior: false, IsAnxious: true, HasSpecialNeeds: false, Notes: "Le gusta el agua");

    private Task<SavePetResult> Save(SavePetCommand command) =>
        new SavePetHandler(_db, new KeyLocalizer()).HandleAsync(command);

    private static (PetPhotoUpload Upload, Func<int> Saves) Photo(string? error = null)
    {
        var saves = 0;
        var upload = new PetPhotoUpload(error, _ =>
        {
            saves++;
            return Task.FromResult("/uploads/pets/new.jpg");
        });
        return (upload, () => saves);
    }

    [Fact]
    public async Task A_new_pet_is_created_with_known_temperaments_and_the_species_photo()
    {
        var result = await Save(Command(_owner.Id, name: "  Toby ", temperaments: [" tranquilo", "Amigable", "TRANQUILO", "Gruñón", ""]));

        Assert.Equal(SavePetStatus.Created, result.Status);
        var pet = await _db.Pets.AsNoTracking().SingleAsync();
        Assert.Equal(("Toby", "Labrador Retriever", 3, PetSize.Large), (pet.Name, pet.Breed, pet.AgeYears, pet.Size));
        Assert.Equal("Tranquilo, Amigable", pet.Temperament);
        Assert.Equal(PetSpecies.DefaultPhoto(PetSpecies.Dog), pet.PhotoUrl);
        Assert.Equal((true, "Le gusta el agua"), (pet.IsAnxious, pet.Notes));
    }

    [Fact]
    public async Task All_field_errors_come_back_together_and_the_photo_is_not_saved()
    {
        var (photo, saves) = Photo("Pets_PhotoInvalid");

        var result = await Save(Command(_owner.Id, name: " ", species: null, age: null, temperaments: ["Gruñón"], photo: photo));

        Assert.Equal(SavePetStatus.Invalid, result.Status);
        Assert.Equal(
            [
                new PetFieldError(PetField.Name, "Pets_NameRequired"),
                new PetFieldError(PetField.Species, "Pets_SpeciesRequired"),
                new PetFieldError(PetField.AgeYears, "Pets_AgeRequired"),
                new PetFieldError(PetField.Temperaments, "Pets_TemperamentRequired"),
                new PetFieldError(PetField.Photo, "Pets_PhotoInvalid")
            ],
            result.Errors);
        Assert.Equal(0, saves());
        Assert.False(await _db.Pets.AnyAsync());
    }

    [Theory]
    [InlineData("Dinosaurio", null, 3, PetField.Species, "Pets_SpeciesInvalid")]
    [InlineData(PetSpecies.Other, " ", 3, PetField.CustomType, "Pets_OtherTypeRequired")]
    [InlineData(PetSpecies.Dog, null, -1, PetField.AgeYears, "Pets_AgeRange")]
    [InlineData(PetSpecies.Dog, null, 41, PetField.AgeYears, "Pets_AgeRange")]
    public async Task Invalid_values_are_reported_on_their_field(string species, string? customType, int age, PetField field, string key)
    {
        var result = await Save(Command(_owner.Id, species: species, customType: customType, age: age));

        Assert.Equal(new PetFieldError(field, key), Assert.Single(result.Errors));
    }

    [Theory]
    [InlineData(PetSpecies.Other, " Hurón ", "Labrador Retriever", null, "Hurón")]
    [InlineData(PetSpecies.Dog, null, "__other__", " Criollo ", "Criollo")]
    [InlineData(PetSpecies.Dog, null, "__other__", null, "Pets_BreedDefault")]
    [InlineData(PetSpecies.Dog, null, " Pastor raro ", null, "Pastor raro")]
    [InlineData(PetSpecies.Dog, null, null, null, "Pets_BreedDefault")]
    public async Task The_breed_comes_from_the_matching_field(string species, string? customType, string? breed, string? customBreed, string expected)
    {
        await Save(Command(_owner.Id, species: species, customType: customType, breed: breed, customBreed: customBreed));

        Assert.Equal(expected, (await _db.Pets.AsNoTracking().SingleAsync()).Breed);
    }

    [Fact]
    public async Task A_second_pet_with_the_same_species_name_and_breed_is_rejected()
    {
        await Save(Command(_owner.Id));
        var (photo, saves) = Photo();

        var result = await Save(Command(_owner.Id, name: "TOBY", breed: "labrador retriever", photo: photo));

        Assert.Equal(new PetFieldError(PetField.Name, "Pets_Duplicate"), Assert.Single(result.Errors));
        Assert.Equal(0, saves());
        Assert.Equal(1, await _db.Pets.CountAsync());
    }

    [Fact]
    public async Task Another_owner_can_use_the_same_name_and_breed()
    {
        await Save(Command(_owner.Id));

        var result = await Save(Command(TestData.AddUser(_db).Id));

        Assert.Equal(SavePetStatus.Created, result.Status);
    }

    [Fact]
    public async Task A_valid_upload_is_saved_and_stored_as_the_photo()
    {
        var (photo, saves) = Photo();

        await Save(Command(_owner.Id, photoUrl: "https://example.com/ignored.jpg", photo: photo));

        Assert.Equal(1, saves());
        Assert.Equal("/uploads/pets/new.jpg", (await _db.Pets.AsNoTracking().SingleAsync()).PhotoUrl);
    }

    [Fact]
    public async Task Editing_keeps_the_photo_and_can_keep_its_own_name()
    {
        await Save(Command(_owner.Id, photoUrl: "https://example.com/toby.jpg"));
        var id = (await _db.Pets.AsNoTracking().SingleAsync()).Id;

        var result = await Save(Command(_owner.Id, petId: id, age: 4, temperaments: ["Juguetón"]));

        Assert.Equal(SavePetStatus.Updated, result.Status);
        var pet = await _db.Pets.AsNoTracking().SingleAsync();
        Assert.Equal((4, "Juguetón", "https://example.com/toby.jpg"), (pet.AgeYears, pet.Temperament, pet.PhotoUrl));
    }

    [Fact]
    public async Task Editing_someone_elses_pet_is_not_found()
    {
        var other = TestData.AddPet(_db, TestData.AddUser(_db));

        var result = await Save(Command(_owner.Id, petId: other.Id));

        Assert.Equal(SavePetStatus.NotFound, result.Status);
        Assert.NotEqual("Toby", (await _db.Pets.AsNoTracking().SingleAsync()).Name);
    }
}
