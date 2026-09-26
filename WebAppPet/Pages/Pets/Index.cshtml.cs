using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using WebAppPet.Application.Pets.DeletePet;
using WebAppPet.Application.Pets.GetPets;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;
using WebAppPet.Ui;

namespace WebAppPet.Pages.Pets;

public class IndexModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetPetsHandler _getPets;
    private readonly DeletePetHandler _deletePet;
    private readonly IStringLocalizer<SharedResource> _L;

    public IndexModel(AuthService auth, GetPetsHandler getPets, DeletePetHandler deletePet, IStringLocalizer<SharedResource> L)
    {
        _auth = auth;
        _getPets = getPets;
        _deletePet = deletePet;
        _L = L;
    }

    public bool IsGuest { get; set; }
    public List<Pet> Pets { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
        {
            IsGuest = true;
            return Page();
        }

        Pets = await _getPets.HandleAsync(new GetPetsQuery(userId));
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = "/Pets" });

        if (await _deletePet.HandleAsync(new DeletePetCommand(userId, id)) == DeletePetResult.Deleted)
            AppFlash.Toast(this, "✓ " + _L["Feedback_PetDeleted"].Value);

        return RedirectToPage();
    }

    public string SizeLabel(PetSize s) => s switch
    {
        PetSize.Small => _L["Size_Small"].Value,
        PetSize.Large => _L["Size_Large"].Value,
        PetSize.Giant => _L["Size_Giant"].Value,
        _ => _L["Size_Medium"].Value
    };
}
