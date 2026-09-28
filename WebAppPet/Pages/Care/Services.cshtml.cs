using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Behavior.StartBehaviorCase;
using WebAppPet.Infrastructure.Identity;

namespace WebAppPet.Pages.Care;

public class ServicesModel : PageModel
{
    private readonly AuthService _auth;
    private readonly StartBehaviorCaseHandler _startBehavior;

    public ServicesModel(AuthService auth, StartBehaviorCaseHandler startBehavior)
    {
        _auth = auth;
        _startBehavior = startBehavior;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostStartBehaviorAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = "/Care/Services" });

        var caseId = await _startBehavior.HandleAsync(new StartBehaviorCaseCommand(userId));
        return RedirectToPage("/Behavior/Intake", new { caseId });
    }
}
