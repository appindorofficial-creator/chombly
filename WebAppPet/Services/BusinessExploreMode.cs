namespace WebAppPet.Services;

/// <summary>
/// Presentation / explore tour (Welcome or Negocios → Explorar servicios).
/// While active, Inicio/Perfil show “Salir de presentación” instead of “Ver presentación”.
/// </summary>
public static class BusinessExploreMode
{
    public const string CookieName = "chombly.tour";

    public static bool IsActive(HttpRequest request) =>
        string.Equals(request.Cookies[CookieName], "1", StringComparison.Ordinal)
        || string.Equals(request.Cookies["chombly.bizExplore"], "1", StringComparison.Ordinal);

    public static void Enable(HttpResponse response)
    {
        response.Cookies.Append(
            CookieName,
            "1",
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(7),
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                HttpOnly = true
            });
        // Drop legacy cookie if present
        response.Cookies.Delete("chombly.bizExplore", new CookieOptions { Path = "/" });
    }

    public static void Clear(HttpResponse response)
    {
        response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
        response.Cookies.Delete("chombly.bizExplore", new CookieOptions { Path = "/" });
    }
}
