namespace WebAppPet.Services;

/// <summary>
/// Shell UX for dual-role accounts (owner vs business), Indor-style.
/// Role stays Groomer for capabilities; this cookie picks which app shell to show.
/// </summary>
public static class AppShellMode
{
    public const string CookieName = "chombly.shell";
    public const string Owner = "owner";
    public const string Business = "business";

    public static string Normalize(string? mode) =>
        string.Equals(mode, Owner, StringComparison.OrdinalIgnoreCase) ? Owner : Business;

    /// <summary>Returns owner, business, or empty if unset.</summary>
    public static string Read(HttpRequest request)
    {
        var v = request.Cookies[CookieName];
        if (string.Equals(v, Owner, StringComparison.OrdinalIgnoreCase)) return Owner;
        if (string.Equals(v, Business, StringComparison.OrdinalIgnoreCase)) return Business;
        return "";
    }

    public static void Set(HttpResponse response, string mode)
    {
        response.Cookies.Append(
            CookieName,
            Normalize(mode),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(14),
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                HttpOnly = true
            });
    }
}
