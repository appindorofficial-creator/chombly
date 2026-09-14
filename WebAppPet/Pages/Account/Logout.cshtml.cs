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
        // location.replace so Back from Welcome does not restore a pre-logout /Index screen.
        const string html =
            """
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <meta http-equiv="refresh" content="0;url=/Welcome" />
              <title>Chombly</title>
              <script>location.replace('/Welcome');</script>
            </head>
            <body>
              <p><a href="/Welcome">Chombly</a></p>
            </body>
            </html>
            """;
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return Content(html, "text/html; charset=utf-8");
    }
}
