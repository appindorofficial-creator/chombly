using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Behavior.GetBehaviorSummary;
using WebAppPet.Application.Behavior.SaveBehaviorFollowUp;
using WebAppPet.Infrastructure.Identity;
using WebAppPet.Localization;
using WebAppPet.Models;

namespace WebAppPet.Pages.Behavior;

public class SummaryModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetBehaviorSummaryHandler _getSummary;
    private readonly SaveBehaviorFollowUpHandler _saveFollowUp;

    public SummaryModel(AuthService auth, GetBehaviorSummaryHandler getSummary, SaveBehaviorFollowUpHandler saveFollowUp)
    {
        _auth = auth;
        _getSummary = getSummary;
        _saveFollowUp = saveFollowUp;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty]
    public string? FollowUpNotes { get; set; }

    public BehaviorCase? Case { get; set; }
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Behavior/Summary/{Id}" });

        Case = await _getSummary.HandleAsync(new GetBehaviorSummaryQuery(userId, Id));
        if (Case is null) return RedirectToPage("/Care/Services");

        FollowUpNotes = Case.FollowUpNotes;
        return Page();
    }

    public async Task<IActionResult> OnPostNoteAsync()
    {
        if (_auth.CurrentUserId is not int userId) return RedirectToPage("/Account/Login");

        Case = await _saveFollowUp.HandleAsync(new SaveBehaviorFollowUpCommand(userId, Id, FollowUpNotes));
        if (Case is null) return RedirectToPage("/Care/Services");

        Message = CatalogLocalizer.Loc("Nota guardada.", "Note saved.");
        FollowUpNotes = Case.FollowUpNotes;
        return Page();
    }
}
