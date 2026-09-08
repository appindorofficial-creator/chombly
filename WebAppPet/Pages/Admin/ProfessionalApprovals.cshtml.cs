using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Admin;

public class ProfessionalApprovalsModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ProfessionalOnboardingService _onboarding;

    public ProfessionalApprovalsModel(AuthService auth, ProfessionalOnboardingService onboarding)
    {
        _auth = auth;
        _onboarding = onboarding;
    }

    public List<ProfessionalOnboardingApplication> Pending { get; set; } = new();
    public string? Message { get; set; }
    public string? Error { get; set; }

    [BindProperty]
    public string? RejectNotes { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!_auth.IsAdmin) return RedirectToPage("/Account/Login");
        Pending = await _onboarding.ListPendingAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(int id)
    {
        if (!_auth.IsAdmin) return RedirectToPage("/Account/Login");
        try
        {
            var app = await _onboarding.ApproveAsync(id, _auth.CurrentUserId!.Value);
            Message = app is null ? "Not found." : $"Approved {app.LegalName} ({app.Track}).";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }

        Pending = await _onboarding.ListPendingAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        if (!_auth.IsAdmin) return RedirectToPage("/Account/Login");
        var notes = string.IsNullOrWhiteSpace(RejectNotes) ? "Needs more documentation." : RejectNotes.Trim();
        var app = await _onboarding.RejectAsync(id, _auth.CurrentUserId!.Value, notes);
        Message = app is null ? "Not found." : $"Rejected {app.LegalName}.";
        Pending = await _onboarding.ListPendingAsync();
        return Page();
    }
}
