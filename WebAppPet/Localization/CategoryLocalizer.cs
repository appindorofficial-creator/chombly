using System.Globalization;
using WebAppPet.Models;

namespace WebAppPet.Localization;

public static class CategoryLocalizer
{
    public static string DisplayName(this ServiceCategory cat)
    {
        if (IsEnglish() && !string.IsNullOrWhiteSpace(cat.NameEn))
            return cat.NameEn;
        return cat.Name;
    }

    public static string DisplaySubtitle(this ServiceCategory cat)
    {
        if (IsEnglish() && !string.IsNullOrWhiteSpace(cat.SubtitleEn))
            return cat.SubtitleEn;
        return cat.Subtitle;
    }

    public static bool IsEnglish()
    {
        var name = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return name.StartsWith("en", StringComparison.OrdinalIgnoreCase);
    }
}
