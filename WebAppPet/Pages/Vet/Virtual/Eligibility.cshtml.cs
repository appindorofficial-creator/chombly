using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.Virtual;

public class EligibilityModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;

    public EligibilityModel(AuthService auth, ConsultationFlowService flow)
    {
        _auth = auth;
        _flow = flow;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    public Consultation? Consultation { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/Virtual/Eligibility?consultationId={ConsultationId}" });

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/Index");
        return Page();
    }
}
