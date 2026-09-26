using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Consultations.GetConsultationSummary;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.Virtual;

/// <summary>
/// Legacy URL — safety is now on Pet. Keep for bookmarks / Emergency back-links.
/// </summary>
public class SafetyModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetConsultationSummaryHandler _getSummary;

    public SafetyModel(AuthService auth, GetConsultationSummaryHandler getSummary)
    {
        _auth = auth;
        _getSummary = getSummary;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Edit { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/Virtual/Pet?consultationId={ConsultationId}" });

        var summary = await _getSummary.HandleAsync(new GetConsultationSummaryQuery(userId, ConsultationId));
        if (summary is null) return RedirectToPage("/Vet/Index");

        var next = ConsultationPath.Normalize(null, summary.Consultation.ServiceCatalogCode);
        return RedirectToPage("/Vet/Virtual/Pet", new { consultationId = ConsultationId, next });
    }

    public IActionResult OnPost() => RedirectToPage("/Vet/Virtual/Pet", new { consultationId = ConsultationId });
}
