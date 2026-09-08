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

    [BindProperty]
    public int AgeYears { get; set; } = 1;

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

    public List<(PetSize Value, string Label, string Emoji)> SizeOptions { get; private set; } = new();

    public IActionResult OnGet()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login");
        FillSizeOptions();
        Temperament = _L["Pets_TemperamentDefault"].Value;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        FillSizeOptions();
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        if (string.IsNullOrWhiteSpace(Name))
            ModelState.AddModelError(nameof(Name), _L["Pets_NameRequired"].Value);

        if (string.IsNullOrWhiteSpace(Species))
            ModelState.AddModelError(nameof(Species), _L["Pets_SpeciesRequired"].Value);
        else if (!PetSpecies.IsKnown(Species))
            ModelState.AddModelError(nameof(Species), _L["Pets_SpeciesInvalid"].Value);
        else if (Species == PetSpecies.Other && string.IsNullOrWhiteSpace(CustomType))
            ModelState.AddModelError(nameof(CustomType), _L["Pets_OtherTypeRequired"].Value);

        if (AgeYears < 0 || AgeYears > 40)
            ModelState.AddModelError(nameof(AgeYears), _L["Pets_AgeRange"].Value);

        var photoError = PetPhotoStorage.Validate(PhotoFile);
        if (photoError is not null)
            ModelState.AddModelError(nameof(PhotoFile), _L[photoError].Value);

        if (!ModelState.IsValid) return Page();

        // Guardar el valor canónico (ES) en BD; la UI traduce con PetSpecies.Label
        var species = Species.Trim();
        var breedDefault = _L["Pets_BreedDefault"].Value;
        string breed;
        if (species == PetSpecies.Other)
        {
            // Custom animal name is required for "Otro"; store it as Breed for display.
            breed = CustomType!.Trim();
        }
        else
        {
            breed = string.IsNullOrWhiteSpace(Breed) ? breedDefault : Breed.Trim();
        }
        var temperamentDefault = _L["Pets_TemperamentDefault"].Value;

        string? photoUrl = null;
        if (PhotoFile is { Length: > 0 })
        {
            photoUrl = await PetPhotoStorage.SaveAsync(PhotoFile, userId, _env, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(PhotoUrl))
        {
            photoUrl = PhotoUrl.Trim();
        }

        _db.Pets.Add(new Pet
        {
            OwnerId = userId,
            Name = Name.Trim(),
            Species = species,
            Breed = breed,
            AgeYears = AgeYears,
            Size = Size,
            Temperament = string.IsNullOrWhiteSpace(Temperament) ? temperamentDefault : Temperament.Trim(),
            PhotoUrl = photoUrl ?? PetSpecies.DefaultPhoto(species),
            IsSenior = IsSenior,
            IsAnxious = IsAnxious,
            HasSpecialNeeds = HasSpecialNeeds,
            Notes = Notes
        });
        await _db.SaveChangesAsync(cancellationToken);
        return RedirectToPage("./Index");
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
