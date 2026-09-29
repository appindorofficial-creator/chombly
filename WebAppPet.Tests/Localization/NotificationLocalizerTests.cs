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
    [InlineData("Aviso creado desde Control. Ajusta fecha o frecuencia si lo necesitas.")]
    [InlineData("Created from Care. Adjust the date or frequency if needed.")]
    public void The_retired_care_note_no_longer_offers_editing(string stored)
    {
        Assert.Equal("Aviso creado desde Control.", In("es", () => NotificationLocalizer.Message(stored)));
        Assert.Equal("Created from Care.", In("en", () => NotificationLocalizer.Message(stored)));
        Assert.Equal("Aviso creado desde Control.", In("es", () => NotificationLocalizer.Reminder(stored)));
        Assert.Equal("Created from Care.", In("en", () => NotificationLocalizer.Reminder(stored)));
    }

    [Theory]
    [InlineData("Onboarding profesional aprobado")]
    [InlineData("Registro profesional aprobado")]
    public void Onboarding_titles_show_in_english(string stored)
    {
        Assert.Equal("Professional onboarding approved", In("en", () => NotificationLocalizer.Title(stored)));
    }

    [Theory]
    [InlineData("Vacunas / chequeo · Green", "Vaccines / checkup · Green")]
    [InlineData("Vacunas / chequeo · Mi Luna", "Vaccines / checkup · Mi Luna")]
    [InlineData("Nota clínica disponible", "Clinical note available")]
    [InlineData("Chombly Care renovado", "Chombly Care renewed")]
    public void Titles_show_in_the_screen_language_whichever_language_was_saved(string es, string en)
    {
        Assert.Equal(es, In("es", () => NotificationLocalizer.Title(en)));
        Assert.Equal(es, In("es", () => NotificationLocalizer.Title(es)));
        Assert.Equal(en, In("en", () => NotificationLocalizer.Title(es)));
        Assert.Equal(en, In("en", () => NotificationLocalizer.Title(en)));
    }

    [Theory]
    [InlineData("Aviso creado desde Control.", "Created from Care.")]
    [InlineData(
        "Guau Spa no pudo aceptar tu cita (25/9/2026 9:03). Te reembolsamos el anticipo.",
        "Guau Spa could not accept your booking (25/9/2026 9:03). Your deposit was refunded.")]
    [InlineData(
        "Guau Spa confirmó tu cita (25/9/2026 9:03).",
        "Guau Spa confirmed your booking (25/9/2026 9:03).")]
    [InlineData(
        "Cancelaste tu cita en Guau Spa. Te reembolsamos el anticipo.",
        "You cancelled your appointment at Guau Spa. Your deposit was refunded.")]
    [InlineData(
        "Cancelamos tu cita en Guau Spa (25/9/2026 9:03) y te reembolsamos $ 20.000.",
        "We cancelled your appointment at Guau Spa (25/9/2026 9:03) and refunded $ 20.000.")]
    [InlineData(
        "Te reembolsamos $ 20.000 de tu cita en Guau Spa (25/9/2026 9:03).",
        "We refunded $ 20.000 for your appointment at Guau Spa (25/9/2026 9:03).")]
    [InlineData("Te reembolsamos $ 20.000.", "We refunded $ 20.000.")]
    [InlineData(
        "Cobramos $ 20.000 a tu tarjeta •••• 4242. Tu próximo cobro es el 25/10/2026.",
        "We charged $ 20.000 to your card •••• 4242. Your next charge is on 25/10/2026.")]
    [InlineData(
        "No pudimos cobrar la renovación a tu tarjeta •••• 4242. Pago rechazado: fondos insuficientes. Prueba con otra tarjeta. Luego activa la membresía de nuevo.",
        "We couldn't charge the renewal to your card •••• 4242. Payment declined: insufficient funds. Try another card. Then reactivate the membership.")]
    [InlineData(
        "Guau Spa guardó una nota en el historial de tu mascota.",
        "Guau Spa saved a note to your pet’s history.")]
    [InlineData("Nota guardada para Rocky.", "Note saved for Rocky.")]
    [InlineData(
        "Tu consulta (VC-12) quedó pendiente de confirmación.",
        "Your consultation (VC-12) is pending confirmation.")]
    [InlineData(
        "Guau Spa vuelve a solicitar publicación.",
        "Guau Spa is requesting publication again.")]
    public void Messages_show_in_the_screen_language_whichever_language_was_saved(string es, string en)
    {
        Assert.Equal(es, In("es", () => NotificationLocalizer.Message(en)));
        Assert.Equal(es, In("es", () => NotificationLocalizer.Message(es)));
        Assert.Equal(en, In("en", () => NotificationLocalizer.Message(es)));
        Assert.Equal(en, In("en", () => NotificationLocalizer.Message(en)));
    }

    [Theory]
    [InlineData(WebAppPet.Application.Payments.Shared.PaymentFailureCodes.InsufficientFunds)]
    [InlineData(WebAppPet.Application.Payments.Shared.PaymentFailureCodes.ExpiredCard)]
    [InlineData(WebAppPet.Application.Payments.Shared.PaymentFailureCodes.ProcessingError)]
    [InlineData(WebAppPet.Application.Payments.Shared.PaymentFailureCodes.CardDeclined)]
    public void Saved_payment_failure_messages_have_the_same_english_as_the_payment_screen(string code)
    {
        var saved = WebAppPet.Application.Payments.Shared.PaymentFailureCodes.SpanishMessage(code);
        Assert.Equal(
            In("en", () => WebAppPet.Application.Payments.Shared.PaymentFailureCodes.Message(code)),
            In("en", () => NotificationLocalizer.Message(saved)));
    }

    [Fact]
    public void Known_leaves_free_text_to_the_caller()
    {
        Assert.Null(In("en", () => NotificationLocalizer.Known("Pastilla del corazón")));
        Assert.Equal("Vaccines / checkup · Rocky", In("en", () => NotificationLocalizer.Known("Vacunas / chequeo · Rocky")));
        Assert.Equal("Vacunas / chequeo · Rocky", In("es", () => NotificationLocalizer.Known("Vaccines / checkup · Rocky")));
    }

    [Theory]
    [InlineData("Prueba hora", "en", "Prueba hora")]
    [InlineData("Antipulgas · Rocky", "en", "Antipulgas · Rocky")]
    [InlineData("Con comida", "en", "Con comida")]
    [InlineData("Recordatorio de vacuna", "en", "Vaccine reminder")]
    [InlineData("Recordatorio de medicamento · Rocky", "en", "Medication reminder · Rocky")]
    [InlineData("Vacuna antirrábica", "en", "Rabies vaccine")]
    [InlineData("Vacunas / chequeo · Rocky", "en", "Vaccines / checkup · Rocky")]
    [InlineData("Vaccine reminder", "es", "Recordatorio de vacuna")]
    [InlineData("Heart pill", "es", "Heart pill")]
    [InlineData("Prueba hora", "es", "Prueba hora")]
    public void Reminder_text_typed_by_the_family_is_shown_as_written(string stored, string culture, string expected)
    {
        Assert.Equal(expected, In(culture, () => NotificationLocalizer.Reminder(stored)));
    }

    [Theory]
    [InlineData("Clinical note: Tomar agua", "es", "Nota clínica: Tomar agua")]
    [InlineData("Nota clínica: Rest two days", "en", "Clinical note: Rest two days")]
    [InlineData("Paseo 60 min · Follow-up: revisar", "es", "Paseo 60 min · Seguimiento: revisar")]
    public void Clinical_note_label_follows_the_screen_and_keeps_the_vet_text(string stored, string culture, string expected)
    {
        Assert.Equal(expected, In(culture, () => CatalogLocalizer.Notes(stored)));
    }
}
