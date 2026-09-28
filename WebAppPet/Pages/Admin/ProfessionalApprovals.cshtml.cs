using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.ProfessionalOnboarding.ApproveOnboarding;
using WebAppPet.Application.ProfessionalOnboarding.GetPendingOnboardings;
using WebAppPet.Application.ProfessionalOnboarding.RejectOnboarding;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Admin;

public class ProfessionalApprovalsModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetPendingOnboardingsHandler _getPending;
    private readonly ApproveOnboardingHandler _approve;
    private readonly RejectOnboardingHandler _reject;

    public ProfessionalApprovalsModel(
        AuthService auth,
        GetPendingOnboardingsHandler getPending,
        ApproveOnboardingHandler approve,
        RejectOnboardingHandler reject)
    {
        _auth = auth;
        _getPending = getPending;
        _approve = approve;
        _reject = reject;
    }

    public List<ProfessionalOnboardingApplication> Pending { get; set; } = new();
    public string? Message { get; set; }
    public string? Error { get; set; }

    [BindProperty]
    public string? RejectNotes { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!_auth.IsAdmin) return RedirectToPage("/Account/Login");
        Pending = await _getPending.HandleAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(int id)
    {
        if (!_auth.IsAdmin) return RedirectToPage("/Account/Login");

        var result = await _approve.HandleAsync(new ApproveOnboardingCommand(id, _auth.CurrentUserId!.Value));
        var name = result.Application?.LegalName;
        switch (result.Outcome)
        {
            case ApproveOnboardingOutcome.NotFound:
                Error = NotFound;
                break;
            case ApproveOnboardingOutcome.NotPending:
                Error = CatalogLocalizer.Loc(
                    $"La solicitud de {name} ya no está pendiente de revisión.",
                    $"{name}'s application is no longer pending review.");
                break;
            case ApproveOnboardingOutcome.NoBusinessProfile:
                Error = CatalogLocalizer.Loc(
                    $"{name} no tiene perfil de negocio. Debe registrar su negocio primero.",
                    $"{name} has no business profile. They must register their business first.");
                break;
            default:
                Message = CatalogLocalizer.Loc(
                    $"Solicitud de {name} aprobada ({TrackLabel(result.Application!.Track)}).",
                    $"{name}'s application approved ({TrackLabel(result.Application!.Track)}).");
                break;
        }

        Pending = await _getPending.HandleAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        if (!_auth.IsAdmin) return RedirectToPage("/Account/Login");
        var notes = string.IsNullOrWhiteSpace(RejectNotes)
            ? "Falta documentación. Complétala y envía la solicitud de nuevo."
            : RejectNotes.Trim();
        var app = await _reject.HandleAsync(new RejectOnboardingCommand(id, _auth.CurrentUserId!.Value, notes));
        if (app is null)
            Error = NotFound;
        else
            Message = CatalogLocalizer.Loc($"Solicitud de {app.LegalName} rechazada.", $"{app.LegalName}'s application rejected.");
        Pending = await _getPending.HandleAsync();
        return Page();
    }

    private static string NotFound => CatalogLocalizer.Loc("No encontramos esa solicitud.", "Application not found.");

    private static string TrackLabel(ProfessionalOnboardingTrack track) => track switch
    {
        ProfessionalOnboardingTrack.International => CatalogLocalizer.Loc("Internacional", "International"),
        ProfessionalOnboardingTrack.Behavior => CatalogLocalizer.Loc("Conducta", "Behavior"),
        _ => CatalogLocalizer.Loc("Local", "Local")
    };
}
