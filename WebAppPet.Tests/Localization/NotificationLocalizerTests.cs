using System.Globalization;
using WebAppPet.Localization;

namespace WebAppPet.Tests.Localization;

public class NotificationLocalizerTests
{
    private static string In(string culture, Func<string> render)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try { return render(); }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Theory]
    [InlineData("Onboarding profesional aprobado", "Registro profesional aprobado")]
    [InlineData("Onboarding profesional no aprobado", "Registro profesional no aprobado")]
    [InlineData("Registro profesional aprobado", "Registro profesional aprobado")]
    public void Onboarding_titles_already_saved_show_the_current_spanish_wording(string stored, string expected)
    {
        Assert.Equal(expected, In("es", () => NotificationLocalizer.Title(stored)));
    }

    [Theory]
    [InlineData("Onboarding profesional aprobado")]
    [InlineData("Registro profesional aprobado")]
    public void Onboarding_titles_show_in_english(string stored)
    {
        Assert.Equal("Professional onboarding approved", In("en", () => NotificationLocalizer.Title(stored)));
    }
}
