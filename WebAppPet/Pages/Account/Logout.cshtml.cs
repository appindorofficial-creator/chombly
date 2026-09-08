using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Services;

namespace WebAppPet.Pages.Account;

public class LogoutModel : PageModel
{
    private readonly AuthService _auth;

    public LogoutModel(AuthService auth) => _auth = auth;

    public async Task<IActionResult> OnGetAsync()
    {
        await _auth.SignOutAsync();
        return RedirectToPage("/Index");
    }
}
