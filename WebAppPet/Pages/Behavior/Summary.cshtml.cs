using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Behavior;

public class SummaryModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly BehaviorFlowService _flow;

    public SummaryModel(AppDbContext db, AuthService auth, BehaviorFlowService flow)
    {
        _db = db;
        _auth = auth;
        _flow = flow;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty]
    public string? FollowUpNotes { get; set; }

    public BehaviorCase? Case { get; set; }
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Behavior/Summary/{Id}" });

        Case = await _flow.GetOwnedAsync(Id);
        if (Case is null) return RedirectToPage("/Care/Services");

        FollowUpNotes = Case.FollowUpNotes;
        return Page();
    }

    public async Task<IActionResult> OnPostNoteAsync()
    {
        if (_auth.CurrentUserId is null) return RedirectToPage("/Account/Login");

        Case = await _flow.GetOwnedAsync(Id);
        if (Case is null) return RedirectToPage("/Care/Services");

        Case.FollowUpNotes = FollowUpNotes?.Trim();
        if (Case.Status < BehaviorCaseStatus.PlanActive)
            Case.Status = BehaviorCaseStatus.PlanActive;
        await _flow.TouchAsync(Case);

        _db.Notifications.Add(new AppNotification
        {
            UserId = _auth.CurrentUserId.Value,
            Title = CatalogLocalizer.Loc("Plan de conducta", "Behavior plan"),
            Message = CatalogLocalizer.Loc(
                $"Nota guardada para {Case.Pet?.Name}.",
                $"Note saved for {Case.Pet?.Name}."),
            Type = "behavior-plan",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        Message = CatalogLocalizer.Loc("Nota guardada.", "Note saved.");
        FollowUpNotes = Case.FollowUpNotes;
        return Page();
    }
}
