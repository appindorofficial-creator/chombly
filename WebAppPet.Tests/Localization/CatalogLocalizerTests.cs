using System.Globalization;
using WebAppPet.Localization;

namespace WebAppPet.Tests.Localization;

public class CatalogLocalizerTests
{
    private static string In(string culture, Func<string> render)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try { return render(); }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Theory]
    [InlineData("Basic bath", "Baño básico")]
    [InlineData("Haircut", "Corte de pelo")]
    [InlineData("Full grooming", "Grooming completo")]
    [InlineData(" full grooming ", "grooming completo")]
    [InlineData("session", "sesión")]
    [InlineData("30-min walk", "Paseo 30 min")]
    [InlineData("Advanced session", "Sesión avanzada")]
    [InlineData("Standard night", "Noche estándar")]
    [InlineData("Half-day", "Medio día")]
    public void Catalog_names_saved_in_english_show_in_spanish(string saved, string expected)
    {
        Assert.Equal(expected, In("es", () => CatalogLocalizer.Text(saved)));
    }

    [Theory]
    [InlineData("Hola")]
    [InlineData("Hotel")]
    [InlineData("Baño básico")]
    [InlineData("Our grooming is the best")]
    [InlineData("Walks")]
    public void Other_text_stays_as_the_business_wrote_it(string saved)
    {
        Assert.Equal(saved, In("es", () => CatalogLocalizer.Text(saved)));
    }

    [Fact]
    public void English_keeps_translating_spanish_catalog_names()
    {
        Assert.Equal("Basic bath", In("en", () => CatalogLocalizer.Text("Baño básico")));
        Assert.Equal("Basic bath", In("en", () => CatalogLocalizer.Text("Basic bath")));
    }
}
