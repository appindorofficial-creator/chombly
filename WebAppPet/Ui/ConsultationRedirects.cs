using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Consultations.Shared;

namespace WebAppPet.Ui;

public static class ConsultationRedirects
{
    /// <param name="next">Path ("local" or "intl") kept when going back to the pet step.</param>
    public static RedirectToPageResult RedirectToStep(this PageModel page, ConsultationStep step, int consultationId, string? next = null) => step switch
    {
        ConsultationStep.VetHome => page.RedirectToPage("/Vet/Index"),
        ConsultationStep.Pet => page.RedirectToPage("/Vet/Virtual/Pet", new { consultationId, next }),
        ConsultationStep.Eligibility => page.RedirectToPage("/Vet/Virtual/Eligibility", new { consultationId }),
        ConsultationStep.LocalProviders => page.RedirectToPage("/Vet/Virtual/Providers", new { consultationId }),
        ConsultationStep.Checkout => page.RedirectToPage("/Vet/Virtual/Checkout", new { consultationId }),
        ConsultationStep.IntlHome => page.RedirectToPage("/Vet/International/Home", new { consultationId }),
        ConsultationStep.IntlMatches => page.RedirectToPage("/Vet/International/Matches", new { consultationId }),
        ConsultationStep.IntlSchedule => page.RedirectToPage("/Vet/International/Consent", new { consultationId }),
        ConsultationStep.ChomblyCare => page.RedirectToPage("/Plans/ChomblyCare", new { consultationId }),
        _ => throw new ArgumentOutOfRangeException(nameof(step), step, null)
    };
}
