using System.Globalization;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Domain.Markets;

namespace WebAppPet.Tests.Application.Payments;

public class PaymentTextsTests
{
    private static string In(string culture, Func<string> render)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try { return render(); }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Theory]
    [InlineData("Deposit · Neiva Spa", "Anticipo · Neiva Spa")]
    [InlineData("Behavior case #7 · Rocky", "Caso de comportamiento #7 · Rocky")]
    [InlineData("Vet consultation #3 (ABC123)", "Consulta veterinaria #3 (ABC123)")]
    [InlineData("Chombly Care · first month", "Chombly Care · primer mes")]
    [InlineData("Chombly Care · renewal", "Chombly Care · renovación")]
    public void System_descriptions_show_in_spanish(string stored, string expected)
    {
        Assert.Equal(expected, In("es", () => PaymentTexts.Description(stored)));
    }

    [Theory]
    [InlineData("Deposit · Neiva Spa")]
    [InlineData("Behavior case #7 · Rocky")]
    [InlineData("Chombly Care · renewal")]
    public void System_descriptions_stay_in_english(string stored)
    {
        Assert.Equal(stored, In("en", () => PaymentTexts.Description(stored)));
    }

    [Theory]
    [InlineData("es", "la familia canceló")]
    [InlineData("en", "the family cancelled")]
    public void Refund_reason_codes_are_readable(string culture, string expected)
    {
        Assert.Equal(expected, In(culture, () => PaymentTexts.RefundReason("client_cancelled")));
    }

    [Fact]
    public void Free_text_refund_reason_is_shown_as_written()
    {
        Assert.Equal("Cliente pidió reembolso", In("en", () => PaymentTexts.RefundReason("Cliente pidió reembolso")));
    }

    [Theory]
    [InlineData("es", "A 2.5 km de ti")]
    [InlineData("en", "2.5 km away")]
    public void Distance_label_follows_the_interface_language(string culture, string expected)
    {
        var label = In(culture, () => GeoHelper.FormatKmAway(2.5)!);
        Assert.Equal(expected, label.Replace(',', '.'));
    }
}
