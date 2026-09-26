using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using WebAppPet.Application.Pets.GetPet;
using WebAppPet.Application.Pets.SavePet;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Pets;

[RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
[RequestSizeLimit(10 * 1024 * 1024)]
public class CreateModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetPetHandler _getPet;
    private readonly SavePetHandler _savePet;
    private readonly IStringLocalizer<SharedResource> _L;
    private readonly IWebHostEnvironment _env;

    public CreateModel(
        AuthService auth,
        GetPetHandler getPet,
        SavePetHandler savePet,
        IStringLocalizer<SharedResource> L,
        IWebHostEnvironment env)
    {
        _auth = auth;
        _getPet = getPet;
        _savePet = savePet;
        _L = L;
        _env = env;
    }

    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    public bool IsEdit => Id is > 0;

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
    public List<string> Temperaments { get; set; } = new();

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

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IReadOnlyList<(string Value, string Label, string LabelEn, string Emoji, string BreedHint, string BreedHintEn)> SpeciesOptions =>
        PetSpecies.All;

    public IReadOnlyList<(string Value, string LabelEn)> TemperamentOptions => PetCatalog.Temperaments;

    public List<(PetSize Value, string Label, string Emoji)> SizeOptions { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");
        FillSizeOptions();

        if (IsEdit)
        {
            var pet = await _getPet.HandleAsync(new GetPetQuery(userId, Id!.Value), cancellationToken);
            if (pet is null)
                return RedirectToPage("./Index");

            Name = pet.Name;
            Species = pet.Species;
            AgeYears = pet.AgeYears;
            Size = pet.Size;
            PhotoUrl = pet.PhotoUrl;
            IsSenior = pet.IsSenior;
            IsAnxious = pet.IsAnxious;
            HasSpecialNeeds = pet.HasSpecialNeeds;
            Notes = pet.Notes;
            Temperaments = (pet.Temperament ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (pet.Species == PetSpecies.Other)
            {
                CustomType = pet.Breed;
            }
            else if (!string.IsNullOrWhiteSpace(pet.Breed)
                     && PetCatalog.BreedsFor(pet.Species).Any(b =>
                         b.Value.Equals(pet.Breed, StringComparison.OrdinalIgnoreCase)))
            {
                Breed = pet.Breed;
            }
            else if (!string.IsNullOrWhiteSpace(pet.Breed))
            {
                Breed = PetCatalog.OtherBreed;
                CustomBreed = pet.Breed;
            }

            return Page();
        }

        Species = PetSpecies.Dog;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        FillSizeOptions();
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        ClearFieldErrors(nameof(Name), nameof(Species), nameof(CustomType), nameof(AgeYears), nameof(PhotoFile), nameof(Size), nameof(Temperaments), nameof(Breed), nameof(CustomBreed));
        if (!ModelState.IsValid) return Page();

        var photo = PhotoFile is { Length: > 0 } file
            ? new PetPhotoUpload(PetPhotoStorage.Validate(file), ct => PetPhotoStorage.SaveAsync(file, userId, _env, ct))
            : null;

        var result = await _savePet.HandleAsync(new SavePetCommand(
            userId, IsEdit ? Id : null, Name, Species, CustomType, Breed, CustomBreed, AgeYears, Size,
            Temperaments, PhotoUrl, photo, IsSenior, IsAnxious, HasSpecialNeeds, Notes), cancellationToken);

        if (result.Status == SavePetStatus.NotFound)
            return RedirectToPage("./Index");

        if (!result.Success)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(FieldName(error.Field), _L[error.MessageKey].Value);
            return Page();
        }

        if (result.Status == SavePetStatus.Created)
        {
            TempData["CelebratePet"] = "1";
            TempData["CelebratePetName"] = result.Pet!.Name;
        }

        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            return LocalRedirect(ReturnUrl);

        return RedirectToPage("./Index");
    }

    private static string FieldName(PetField field) => field switch
    {
        PetField.Name => nameof(Name),
        PetField.Species => nameof(Species),
        PetField.CustomType => nameof(CustomType),
        PetField.AgeYears => nameof(AgeYears),
        PetField.Temperaments => nameof(Temperaments),
        _ => nameof(PhotoFile)
    };

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
