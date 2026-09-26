using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Application.Consultations.StartConsultation;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet;

public class IndexModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ClientHomeCountry _homeCountry;
    private readonly StartConsultationHandler _start;

    public IndexModel(AuthService auth, ClientHomeCountry homeCountry, StartConsultationHandler start)
    {
        _auth = auth;
        _homeCountry = homeCountry;
        _start = start;
    }

    [BindProperty(SupportsGet = true)]
    public int? PetId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string BackHref { get; private set; } = "/";
    public string HomeCountryCode { get; private set; } = MarketCountry.DefaultIso;
    public bool ShowUsLocalConsult => MarketCountry.IsUnitedStates(HomeCountryCode);

    public async Task<IActionResult> OnGetAsync()
    {
        BackHref = !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
            ? ReturnUrl!
            : Url.Page("/Index") ?? "/";

        if (_auth.CurrentUserId is int userId)
            HomeCountryCode = await _homeCountry.ResolveAsync(userId);

        return Page();
    }

    /// <summary>Legacy entry — treat as guidance (shortest virtual path).</summary>
    public Task<IActionResult> OnPostStartVirtualAsync() => StartAsync(wantsLocal: false);

    public Task<IActionResult> OnPostStartGuidanceAsync() => StartAsync(wantsLocal: false);

    public Task<IActionResult> OnPostStartLocalAsync() => StartAsync(wantsLocal: true);

    private async Task<IActionResult> StartAsync(bool wantsLocal)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = "/Vet" });

        var started = await _start.HandleAsync(new StartConsultationCommand(userId, wantsLocal, PetId));
        return RedirectToPage("/Vet/Virtual/Pet", new { consultationId = started.ConsultationId, next = started.Path });
    }
}
