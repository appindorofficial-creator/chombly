using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.ProfessionalOnboarding.ApproveOnboarding;
using WebAppPet.Application.ProfessionalOnboarding.GetPendingOnboardings;
using WebAppPet.Application.ProfessionalOnboarding.RejectOnboarding;
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
        switch (result.Outcome)
        {
            case ApproveOnboardingOutcome.NotFound:
                Message = "Not found.";
                break;
            case ApproveOnboardingOutcome.NoBusinessProfile:
                Error = "Applicant has no business profile. Register as business first.";
                break;
            default:
                Message = $"Approved {result.Application!.LegalName} ({result.Application.Track}).";
                break;
        }

        Pending = await _getPending.HandleAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        if (!_auth.IsAdmin) return RedirectToPage("/Account/Login");
        var notes = string.IsNullOrWhiteSpace(RejectNotes) ? "Needs more documentation." : RejectNotes.Trim();
        var app = await _reject.HandleAsync(new RejectOnboardingCommand(id, _auth.CurrentUserId!.Value, notes));
        Message = app is null ? "Not found." : $"Rejected {app.LegalName}.";
        Pending = await _getPending.HandleAsync();
        return Page();
    }
}
