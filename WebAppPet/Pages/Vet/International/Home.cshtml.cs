using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Consultations.FindIntlVet;
using WebAppPet.Application.Consultations.GetIntlHome;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Services;
using WebAppPet.Ui;

namespace WebAppPet.Pages.Vet.International;

public class HomeModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetIntlHomeHandler _getHome;
    private readonly FindIntlVetHandler _findVet;

    public HomeModel(AuthService auth, GetIntlHomeHandler getHome, FindIntlVetHandler findVet)
    {
        _auth = auth;
        _getHome = getHome;
        _findVet = findVet;
    }

    [BindProperty(SupportsGet = true)]
    public int? ConsultationId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? PetId { get; set; }

    public string? PetName { get; set; }
    public string? PetBreed { get; set; }
    public string PreferredLanguage { get; set; } = "es";
    public string LanguageLabel =>
        string.Equals(PreferredLanguage, "en", StringComparison.OrdinalIgnoreCase) ? "English" : "Español";

    public async Task OnGetAsync()
    {
        var home = await _getHome.HandleAsync(new GetIntlHomeQuery(_auth.CurrentUserId, ConsultationId, PetId));
        PreferredLanguage = home.PreferredLanguage;
        PetName = home.PetName;
        PetBreed = home.PetBreed;
    }

    public async Task<IActionResult> OnPostFindAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = "/Vet/International/Home" });

        var result = await _findVet.HandleAsync(new FindIntlVetCommand(userId, ConsultationId, PetId));
        return this.RedirectToStep(result.Next, result.ConsultationId, ConsultationPath.Intl);
    }
}
