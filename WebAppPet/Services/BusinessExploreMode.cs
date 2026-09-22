namespace WebAppPet.Services;

/// <summary>
/// Guest preview of the business panel (Welcome → Negocios → Explorar servicios).
/// </summary>
public static class BusinessExploreMode
{
    public const string CookieName = "chombly.bizExplore";

    public static bool IsActive(HttpRequest request) =>
        string.Equals(request.Cookies[CookieName], "1", StringComparison.Ordinal);

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
    }

    public static void Clear(HttpResponse response) =>
        response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
}
