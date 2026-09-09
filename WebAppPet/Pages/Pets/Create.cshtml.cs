using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Pets;

[RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
[RequestSizeLimit(10 * 1024 * 1024)]
public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly IStringLocalizer<SharedResource> _L;
    private readonly IWebHostEnvironment _env;

    public CreateModel(
        AppDbContext db,
        AuthService auth,
        IStringLocalizer<SharedResource> L,
        IWebHostEnvironment env)
    {
        _db = db;
        _auth = auth;
        _L = L;
        _env = env;
    }

    [BindProperty, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [BindProperty, MaxLength(40)]
    public string Species { get; set; } = string.Empty;

    [BindProperty, MaxLength(80)]
    public string? CustomType { get; set; }

    [BindProperty, MaxLength(80)]
    public string? Breed { get; set; }

    /// <summary>When breed select is "other", free-text breed.</summary>
    [BindProperty, MaxLength(80)]
    public string? CustomBreed { get; set; }

    [BindProperty]
    public int? AgeYears { get; set; }

    [BindProperty]
    public PetSize Size { get; set; } = PetSize.Medium;

    [BindProperty]
    public string Temperament { get; set; } = string.Empty;

    [BindProperty]
    public string? PhotoUrl { get; set; }

    [BindProperty]
    public IFormFile? PhotoFile { get; set; }

    [BindProperty]
    public bool IsSenior { get; set; }

    [BindProperty]
    public bool IsAnxious { get; set; }

    [BindProperty]
    public bool HasSpecialNeeds { get; set; }

    [BindProperty]
    public string? Notes { get; set; }

    public IReadOnlyList<(string Value, string Label, string LabelEn, string Emoji, string BreedHint, string BreedHintEn)> SpeciesOptions =>
        PetSpecies.All;

    public IReadOnlyList<(string Value, string LabelEn)> TemperamentOptions => PetCatalog.Temperaments;

    public List<(PetSize Value, string Label, string Emoji)> SizeOptions { get; private set; } = new();

    public IActionResult OnGet()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login");
        FillSizeOptions();
        Species = PetSpecies.Dog;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        FillSizeOptions();
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        ClearFieldErrors(nameof(Name), nameof(Species), nameof(CustomType), nameof(AgeYears), nameof(PhotoFile), nameof(Size), nameof(Temperament), nameof(Breed), nameof(CustomBreed));

        if (string.IsNullOrWhiteSpace(Name))
            ModelState.AddModelError(nameof(Name), _L["Pets_NameRequired"].Value);

        if (string.IsNullOrWhiteSpace(Species))
            ModelState.AddModelError(nameof(Species), _L["Pets_SpeciesRequired"].Value);
        else if (!PetSpecies.IsKnown(Species))
            ModelState.AddModelError(nameof(Species), _L["Pets_SpeciesInvalid"].Value);
        else if (Species == PetSpecies.Other && string.IsNullOrWhiteSpace(CustomType))
            ModelState.AddModelError(nameof(CustomType), _L["Pets_OtherTypeRequired"].Value);

        var age = AgeYears;
        if (age is null)
            ModelState.AddModelError(nameof(AgeYears), _L["Pets_AgeRequired"].Value);
        else if (age < 0 || age > 40)
            ModelState.AddModelError(nameof(AgeYears), _L["Pets_AgeRange"].Value);

        if (string.IsNullOrWhiteSpace(Temperament)
            || !PetCatalog.Temperaments.Any(t => t.Value.Equals(Temperament, StringComparison.OrdinalIgnoreCase)))
            ModelState.AddModelError(nameof(Temperament), _L["Pets_TemperamentRequired"].Value);

        var photoError = PetPhotoStorage.Validate(PhotoFile);
        if (photoError is not null)
            ModelState.AddModelError(nameof(PhotoFile), _L[photoError].Value);

        if (!ModelState.IsValid) return Page();

        var species = Species.Trim();
        var breedDefault = _L["Pets_BreedDefault"].Value;
        string breed;
        if (species == PetSpecies.Other)
        {
            breed = CustomType!.Trim();
        }
        else if (string.Equals(Breed, PetCatalog.OtherBreed, StringComparison.OrdinalIgnoreCase))
        {
            breed = string.IsNullOrWhiteSpace(CustomBreed) ? breedDefault : CustomBreed.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(Breed)
                 && PetCatalog.BreedsFor(species).Any(b => b.Value.Equals(Breed, StringComparison.OrdinalIgnoreCase)))
        {
            breed = Breed.Trim();
        }
        else
        {
            breed = string.IsNullOrWhiteSpace(Breed) ? breedDefault : Breed.Trim();
        }

        string? photoUrl = null;
        if (PhotoFile is { Length: > 0 })
        {
            photoUrl = await PetPhotoStorage.SaveAsync(PhotoFile, userId, _env, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(PhotoUrl))
        {
            photoUrl = PhotoUrl.Trim();
        }

        var temperament = PetCatalog.Temperaments
            .First(t => t.Value.Equals(Temperament, StringComparison.OrdinalIgnoreCase)).Value;

        _db.Pets.Add(new Pet
        {
            OwnerId = userId,
            Name = Name.Trim(),
            Species = species,
            Breed = breed,
            AgeYears = age!.Value,
            Size = Size,
            Temperament = temperament,
            PhotoUrl = photoUrl ?? PetSpecies.DefaultPhoto(species),
            IsSenior = IsSenior,
            IsAnxious = IsAnxious,
            HasSpecialNeeds = HasSpecialNeeds,
            Notes = Notes
        });
        await _db.SaveChangesAsync(cancellationToken);
        return RedirectToPage("./Index");
    }

    private void ClearFieldErrors(params string[] keys)
    {
        foreach (var key in keys)
        {
            if (ModelState.ContainsKey(key))
                ModelState[key]!.Errors.Clear();
        }
    }

    private void FillSizeOptions()
    {
        SizeOptions = new()
        {
            (PetSize.Small, _L["Size_Small"].Value, "S"),
            (PetSize.Medium, _L["Size_Medium"].Value, "M"),
            (PetSize.Large, _L["Size_Large"].Value, "L"),
            (PetSize.Giant, _L["Size_Giant"].Value, "XL")
        };
    }
}
