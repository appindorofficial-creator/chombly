using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Businesses.ApproveBusiness;
using WebAppPet.Application.Businesses.GetPendingBusinesses;
using WebAppPet.Application.Businesses.RejectBusiness;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Identity;
using WebAppPet.Localization;

namespace WebAppPet.Pages.Admin;

public class ApprovalsModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ApproveBusinessHandler _approve;
    private readonly RejectBusinessHandler _reject;
    private readonly GetPendingBusinessesHandler _getPending;

    public ApprovalsModel(
        AuthService auth,
        ApproveBusinessHandler approve,
        RejectBusinessHandler reject,
        GetPendingBusinessesHandler getPending)
    {
        _auth = auth;
        _approve = approve;
        _reject = reject;
        _getPending = getPending;
    }

    public List<GroomerProfile> Pending { get; set; } = new();
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!_auth.IsAdmin) return RedirectToPage("/Account/Login");
        Pending = await _getPending.HandleAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(int id)
    {
        if (!_auth.IsAdmin) return RedirectToPage("/Account/Login");
        if (await _approve.HandleAsync(new ApproveBusinessCommand(id)) is { } name)
            Message = CatalogLocalizer.Loc($"{name} aprobado y publicado.", $"{name} approved and published.");
        Pending = await _getPending.HandleAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        if (!_auth.IsAdmin) return RedirectToPage("/Account/Login");
        if (await _reject.HandleAsync(new RejectBusinessCommand(id)) is { } name)
            Message = CatalogLocalizer.Loc($"{name} rechazado.", $"{name} rejected.");
        Pending = await _getPending.HandleAsync();
        return Page();
    }
}
