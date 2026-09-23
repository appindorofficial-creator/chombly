using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.Virtual;

/// <summary>
/// Legacy URL — safety is now on Pet. Keep for bookmarks / Emergency back-links.
/// </summary>
public class SafetyModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;

    public SafetyModel(AuthService auth, ConsultationFlowService flow)
    {
        _auth = auth;
        _flow = flow;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Edit { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/Virtual/Pet?consultationId={ConsultationId}" });

        var c = await _flow.GetOwnedAsync(ConsultationId);
        if (c is null) return RedirectToPage("/Vet/Index");

        var next = string.Equals(c.ServiceCatalogCode, ServiceCatalogCodes.VetLocal30, StringComparison.OrdinalIgnoreCase)
            ? "local"
            : "intl";

        // Force the form (clear soft-choice) when Edit=true by resetting status lightly.
        if (Edit && c.HasRedFlags && c.Status == ConsultationStatus.SafetyScreened)
        {
            // Pet shows form when not Escalated; clear Escalated only.
        }

        return RedirectToPage("/Vet/Virtual/Pet", new { consultationId = ConsultationId, next });
    }

    public IActionResult OnPost() => RedirectToPage("/Vet/Virtual/Pet", new { consultationId = ConsultationId });
}
