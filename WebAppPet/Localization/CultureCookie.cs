using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace WebAppPet.Localization;

public static class CultureCookie
{
    public static void Set(HttpResponse response, string culture)
    {
        culture = Normalize(culture);
        response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });
    }

    public static string Normalize(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture)) return "es";
        culture = culture.Trim().ToLowerInvariant();
        return culture.StartsWith("en") ? "en" : "es";
    }

    public static bool IsEnglish() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .StartsWith("en", StringComparison.OrdinalIgnoreCase);
}
