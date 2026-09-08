using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Services;

namespace WebAppPet.Pages.Care;

public class ServicesModel : PageModel
{
    private readonly AuthService _auth;
    private readonly BehaviorFlowService _behavior;

    public ServicesModel(AuthService auth, BehaviorFlowService behavior)
    {
        _auth = auth;
        _behavior = behavior;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostStartBehaviorAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = "/Care/Services" });

        var c = await _behavior.StartAsync();
        return RedirectToPage("/Behavior/Intake", new { caseId = c.Id });
    }
}
