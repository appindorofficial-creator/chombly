using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Localization;
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

    /// <summary>Force the checklist form (skip path choice) so the user can change answers.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Edit { get; set; }

    [BindProperty] public bool BreathingTrouble { get; set; }
    [BindProperty] public bool Seizures { get; set; }
    [BindProperty] public bool Unconscious { get; set; }
    [BindProperty] public bool SevereBleeding { get; set; }
    [BindProperty] public bool ToxinIngestion { get; set; }
    [BindProperty] public bool ExtremePain { get; set; }
    [BindProperty] public bool CannotUrinate { get; set; }
    [BindProperty] public bool NoRedFlags { get; set; }
    [BindProperty] public string? SafetyNotes { get; set; }

    public Consultation? Consultation { get; set; }
    public bool HasVcpr { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>After red flags are saved: let the user choose emergency vs continue virtual.</summary>
    public bool ShowPathChoice { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/Virtual/Safety?consultationId={ConsultationId}" });

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation?.PetId is null) return RedirectToPage("/Vet/Virtual/Pet", new { consultationId = ConsultationId });

        HasVcpr = await _vcpr.HasActiveAsync(Consultation.PetId.Value, Consultation.PetUsState);

        HydrateAnswers(Consultation);

        // After red flags (or return from Emergency): soft choice unless user asked to edit.
        if (!Edit
            && Consultation.HasRedFlags
            && Consultation.Status is ConsultationStatus.SafetyScreened or ConsultationStatus.EscalatedToEmergency)
        {
            ShowPathChoice = true;
        }

        return Page();
    }

    private void HydrateAnswers(Consultation c)
    {
        var a = _safety.Deserialize(c.SafetyAnswersJson);
        if (a is null) return;
        BreathingTrouble = a.BreathingTrouble;
        Seizures = a.Seizures;
        Unconscious = a.Unconscious;
        SevereBleeding = a.SevereBleeding;
        ToxinIngestion = a.ToxinIngestion;
        ExtremePain = a.ExtremePain;
        CannotUrinate = a.CannotUrinate;
        SafetyNotes = a.Notes;
        NoRedFlags = !c.HasRedFlags;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is null) return RedirectToPage("/Account/Login");

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation?.PetId is null) return RedirectToPage("/Vet/Virtual/Pet", new { consultationId = ConsultationId });

        HasVcpr = await _vcpr.HasActiveAsync(Consultation.PetId.Value, Consultation.PetUsState);

        var answers = new SafetyScreeningService.SafetyAnswers(
            BreathingTrouble, Seizures, Unconscious, SevereBleeding,
            ToxinIngestion, ExtremePain, CannotUrinate, SafetyNotes);

        var hasRed = _safety.HasRedFlags(answers);
        if (!hasRed && !NoRedFlags)
        {
            ErrorMessage = CatalogLocalizer.Loc(
                "Marca una bandera roja o confirma “Ninguna de estas” para continuar.",
                "Check a red flag or confirm “None of these” to continue.");
            return Page();
        }

        if (hasRed && NoRedFlags)
            NoRedFlags = false;

        Consultation.SafetyAnswersJson = _safety.Serialize(answers);
        Consultation.HasRedFlags = hasRed;
        Consultation.HasActiveVcpr = await _vcpr.HasActiveAsync(Consultation.PetId.Value, Consultation.PetUsState);
        Consultation.Status = ConsultationStatus.SafetyScreened;
        await _flow.TouchAsync(Consultation);

        if (hasRed)
        {
            await _audit.LogAsync("safety_flagged", _auth.CurrentUserId, "Consultation", Consultation.Id, answers);
            ShowPathChoice = true;
            return Page();
        }

        return await ContinueAfterClearSafetyAsync(Consultation);
    }

    public async Task<IActionResult> OnPostGoEmergencyAsync()
    {
        if (_auth.CurrentUserId is null) return RedirectToPage("/Account/Login");

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/Index");

        await _audit.LogAsync("safety_chose_emergency", _auth.CurrentUserId, "Consultation", Consultation.Id,
            new { consultationId = ConsultationId });
        return RedirectToPage("/Vet/Emergency", new { consultationId = ConsultationId });
    }

    public async Task<IActionResult> OnPostContinueVirtualAsync()
    {
        if (_auth.CurrentUserId is null) return RedirectToPage("/Account/Login");

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation?.PetId is null) return RedirectToPage("/Vet/Virtual/Pet", new { consultationId = ConsultationId });

        HasVcpr = await _vcpr.HasActiveAsync(Consultation.PetId.Value, Consultation.PetUsState);

        // Keep HasRedFlags for clinical context; resume Virtual as guidance (not emergency booking).
        if (Consultation.Status == ConsultationStatus.EscalatedToEmergency)
            Consultation.Status = ConsultationStatus.SafetyScreened;
        Consultation.Modality = VetModality.Virtual;
        await _flow.TouchAsync(Consultation);

        await _audit.LogAsync("safety_chose_continue_virtual", _auth.CurrentUserId, "Consultation", Consultation.Id,
            new { hasRedFlags = Consultation.HasRedFlags });

        return await ContinueAfterClearSafetyAsync(Consultation);
    }

    private Task<IActionResult> ContinueAfterClearSafetyAsync(Consultation c)
    {
        // Always offer Service next. VCPR is enforced only when picking local clinical care
        // (VetLocal30), not for guidance / international orientation.
        return Task.FromResult<IActionResult>(
            RedirectToPage("/Vet/Virtual/Service", new { consultationId = ConsultationId }));
    }
}
