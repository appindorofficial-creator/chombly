using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Shared;

public sealed class BookingPetPickerModel
{
    public required IReadOnlyList<Pet> Pets { get; init; }
    public int PetId { get; init; }
}

public sealed class BookingGateNoteModel
{
    public required string Id { get; init; }
    public required string TextEs { get; init; }
    public required string TextEn { get; init; }
}

public sealed class BookingChooseActionModel
{
    public bool CanSelect { get; init; }
    public bool Selected { get; init; }
    public string? ChooseUrl { get; init; }
    public required string GateAnchorId { get; init; }
    public required string ChooseLabelEs { get; init; }
    public required string ChooseLabelEn { get; init; }
    public string SelectedLabelEs { get; init; } = "Seleccionado ✓";
    public string SelectedLabelEn { get; init; } = "Selected ✓";
}

public sealed class BookingDateFieldModel
{
    public string? Date { get; init; }
    public required string MinDate { get; init; }
}

public sealed class BookingSummaryLine
{
    public required string LabelEs { get; init; }
    public required string LabelEn { get; init; }
    public required string Value { get; init; }
}

public sealed class BookingSummaryHidden
{
    public required string Name { get; init; }
    public string? Value { get; init; }
}

public sealed class BookingSummaryMultiHidden
{
    public required string Name { get; init; }
    public required IEnumerable<string> Values { get; init; }
}

/// <summary>Shared bottom confirm/reserve sheet for Hotel / Walkers / Daycare / Trainers.</summary>
public sealed class BookingSummarySheetModel
{
    public required string BodyId { get; init; }
    public bool StartCollapsed { get; init; }

    public required string TitleEs { get; init; }
    public required string TitleEn { get; init; }
    public required string EditHintEs { get; init; }
    public required string EditHintEn { get; init; }

    public required string BusinessName { get; init; }
    public string? ImageUrl { get; init; }
    public required string FallbackImage { get; init; }
    public required string Subtitle { get; init; }

    public IReadOnlyList<BookingSummaryLine>? Lines { get; init; }
    public string TotalLabelEs { get; init; } = "Total";
    public string TotalLabelEn { get; init; } = "Total";
    public decimal Estimate { get; init; }
    public string? NoteEs { get; init; }
    public string? NoteEn { get; init; }

    public string PaymentHeadingEs { get; init; } = "Pago";
    public string PaymentHeadingEn { get; init; } = "Payment";
    public PaymentMethod? DefaultPayment { get; init; }
    public bool AcceptTerms { get; init; }
    public string? TermsStep { get; init; }

    public required string SubmitLabelEs { get; init; }
    public required string SubmitLabelEn { get; init; }
    public bool ShowSecureLockOnSubmit { get; init; }
    public bool ShowSecureFooter { get; init; } = true;

    public IReadOnlyList<BookingSummaryHidden> HiddenFields { get; init; } = [];
    public IReadOnlyList<BookingSummaryMultiHidden> MultiHiddenFields { get; init; } = [];
}

/// <summary>Shared date helpers for Walkers / Daycare / Trainers booking flows.</summary>
public static class BookingDate
{
    public static bool TryParseSelected(string? date, out DateTime day)
    {
        day = default;
        if (string.IsNullOrWhiteSpace(date) || !DateTime.TryParse(date, out var parsed))
            return false;
        var today = AppTimeZones.TodayLocalDate();
        if (parsed.Date < today) return false;
        day = parsed.Date;
        return true;
    }

    public static string FormatLabel(DateTime day)
    {
        var today = AppTimeZones.TodayLocalDate();
        var datePart = day.ToString("d MMM yyyy");
        if (day.Date == today)
            return $"{CatalogLocalizer.Loc("Hoy", "Today")}, {datePart}";
        if (day.Date == today.AddDays(1))
            return $"{CatalogLocalizer.Loc("Mañana", "Tomorrow")}, {datePart}";
        return day.ToString("ddd d MMM yyyy");
    }

    /// <summary>
    /// Maps legacy When=hoy|manana|fecha + Date into a single Date value (yyyy-MM-dd).
    /// Returns cleared When and normalized Date.
    /// </summary>
    public static (string When, string? Date) NormalizeFromLegacy(string? when, string? date)
    {
        var today = AppTimeZones.TodayLocalDate();
        var key = (when ?? "").Trim().ToLowerInvariant();

        if (key is "hoy" or "today")
            return ("", today.ToString("yyyy-MM-dd"));

        if (key is "manana" or "mañana" or "tomorrow")
            return ("", today.AddDays(1).ToString("yyyy-MM-dd"));

        if (TryParseSelected(date, out var day))
            return ("", day.ToString("yyyy-MM-dd"));

        return ("", null);
    }
}
