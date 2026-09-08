using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet;

public class IndexModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;
    private readonly VetAuditService _audit;

    public IndexModel(AuthService auth, ConsultationFlowService flow, VetAuditService audit)
    {
        _auth = auth;
        _flow = flow;
        _audit = audit;
    }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostStartVirtualAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = "/Vet" });

        var c = await _flow.StartVirtualAsync();
        await _audit.LogAsync("modality_selected", _auth.CurrentUserId, "Consultation", c.Id,
            new { modality = nameof(VetModality.Virtual) });
        return RedirectToPage("/Vet/Virtual/Pet", new { consultationId = c.Id });
    }
}
