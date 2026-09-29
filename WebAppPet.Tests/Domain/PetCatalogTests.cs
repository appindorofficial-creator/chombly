using System.Globalization;
using WebAppPet.Domain;

namespace WebAppPet.Tests.Domain;

public class PetCatalogTests
{
    private static string In(string culture, Func<string> render)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try { return render(); }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Theory]
    [InlineData("es", "Bulldog francés")]
    [InlineData("en", "French Bulldog")]
    public void Breeds_stored_with_english_names_show_in_the_interface_language(string culture, string expected)
    {
        Assert.Equal(expected, In(culture, () => PetCatalog.DisplayBreed(PetSpecies.Dog, "French Bulldog")));
    }

    [Theory]
    [InlineData("es", "Pastor alemán")]
    [InlineData("en", "German Shepherd")]
    public void Spanish_breed_values_keep_translating(string culture, string expected)
    {
        Assert.Equal(expected, In(culture, () => PetCatalog.DisplayBreed(PetSpecies.Dog, "Pastor alemán")));
    }
}
