using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.International;

/// <summary>Legacy — purchase choice is shown on Matches; go to schedule or list.</summary>
public class PurchaseOptionModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;

    public PurchaseOptionModel(AuthService auth, ConsultationFlowService flow)
    {
        _auth = auth;
        _flow = flow;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/International/Matches?consultationId={ConsultationId}" });

        var c = await _flow.GetOwnedAsync(ConsultationId);
        if (c?.ProviderId is int)
            return RedirectToPage("/Vet/International/Consent", new { consultationId = ConsultationId });

        return RedirectToPage("/Vet/International/Matches", new { consultationId = ConsultationId });
    }

    public IActionResult OnPost() => RedirectToPage("/Vet/International/Matches", new { consultationId = ConsultationId });
}
