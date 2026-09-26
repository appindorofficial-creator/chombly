using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Consultations.CheckEligibility;
using WebAppPet.Models;
using WebAppPet.Services;
using WebAppPet.Ui;

namespace WebAppPet.Pages.Vet.Virtual;

public class EligibilityModel : PageModel
{
    private readonly AuthService _auth;
    private readonly CheckEligibilityHandler _checkEligibility;

    public EligibilityModel(AuthService auth, CheckEligibilityHandler checkEligibility)
    {
        _auth = auth;
        _checkEligibility = checkEligibility;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    public Consultation? Consultation { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/Virtual/Eligibility?consultationId={ConsultationId}" });

        var check = await _checkEligibility.HandleAsync(new CheckEligibilityQuery(userId, ConsultationId));
        if (check.Redirect is { } step)
            return this.RedirectToStep(step, ConsultationId);

        Consultation = check.Consultation;
        return Page();
    }
}
