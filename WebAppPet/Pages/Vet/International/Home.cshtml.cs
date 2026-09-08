using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.International;

public class HomeModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;
    private readonly VetAuditService _audit;

    public HomeModel(AuthService auth, ConsultationFlowService flow, VetAuditService audit)
    {
        _auth = auth;
        _flow = flow;
        _audit = audit;
    }

    [BindProperty(SupportsGet = true)]
    public int? ConsultationId { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostFindAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = "/Vet/International/Home" });

        var c = ConsultationId is int id
            ? await _flow.GetOwnedAsync(id)
            : null;
        c ??= await _flow.StartVirtualAsync();
        c.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
        await _flow.TouchAsync(c);
        await _audit.LogAsync("intl_landing_view", _auth.CurrentUserId, "Consultation", c.Id);

        // Ensure pet + location before matching (PDF: mascota y ubicación en cada consulta)
        if (c.PetId is null || string.IsNullOrWhiteSpace(c.PetUsState))
            return RedirectToPage("/Vet/Virtual/Pet", new { consultationId = c.Id, next = "intl" });

        return RedirectToPage("/Vet/International/MatchMode", new { consultationId = c.Id });
    }
}
