using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.Virtual;

public class SafetyModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;
    private readonly SafetyScreeningService _safety;
    private readonly VcprService _vcpr;
    private readonly VetAuditService _audit;

    public SafetyModel(
        AuthService auth,
        ConsultationFlowService flow,
        SafetyScreeningService safety,
        VcprService vcpr,
        VetAuditService audit)
    {
        _auth = auth;
        _flow = flow;
        _safety = safety;
        _vcpr = vcpr;
        _audit = audit;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    [BindProperty] public bool BreathingTrouble { get; set; }
    [BindProperty] public bool Seizures { get; set; }
    [BindProperty] public bool Unconscious { get; set; }
    [BindProperty] public bool SevereBleeding { get; set; }
    [BindProperty] public bool ToxinIngestion { get; set; }
    [BindProperty] public bool ExtremePain { get; set; }
    [BindProperty] public bool CannotUrinate { get; set; }
    [BindProperty] public string? SafetyNotes { get; set; }

    public Consultation? Consultation { get; set; }
    public bool HasVcpr { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/Virtual/Safety?consultationId={ConsultationId}" });

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation?.PetId is null) return RedirectToPage("/Vet/Virtual/Pet", new { consultationId = ConsultationId });

        HasVcpr = await _vcpr.HasActiveAsync(Consultation.PetId.Value, Consultation.PetUsState);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is null) return RedirectToPage("/Account/Login");

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation?.PetId is null) return RedirectToPage("/Vet/Virtual/Pet", new { consultationId = ConsultationId });

        var answers = new SafetyScreeningService.SafetyAnswers(
            BreathingTrouble, Seizures, Unconscious, SevereBleeding,
            ToxinIngestion, ExtremePain, CannotUrinate, SafetyNotes);

        Consultation.SafetyAnswersJson = _safety.Serialize(answers);
        Consultation.HasRedFlags = _safety.HasRedFlags(answers);
        Consultation.HasActiveVcpr = await _vcpr.HasActiveAsync(Consultation.PetId.Value, Consultation.PetUsState);
        Consultation.Status = ConsultationStatus.SafetyScreened;
        await _flow.TouchAsync(Consultation);

        if (Consultation.HasRedFlags)
        {
            await _audit.LogAsync("safety_flagged", _auth.CurrentUserId, "Consultation", Consultation.Id, answers);
            return RedirectToPage("/Vet/Emergency", new { consultationId = ConsultationId });
        }

        if (!Consultation.HasActiveVcpr)
            return RedirectToPage("/Vet/Virtual/Eligibility", new { consultationId = ConsultationId });

        Consultation.Status = ConsultationStatus.EligibilityVerified;
        await _flow.TouchAsync(Consultation);
        return RedirectToPage("/Vet/Virtual/Service", new { consultationId = ConsultationId });
    }
}
