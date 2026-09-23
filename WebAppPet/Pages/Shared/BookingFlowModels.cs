using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;
using Microsoft.AspNetCore.Http;

namespace WebAppPet.Pages.Shared;

public sealed class BookingPetPickerModel
{
    public required IReadOnlyList<Pet> Pets { get; init; }
    public int PetId { get; init; }
    /// <summary>When set, "Add pet" links return here after create.</summary>
    public string? ReturnUrl { get; init; }
}

/// <summary>Multi-pet switch picker (Hotel / Booking / Daycare / Walkers / Trainers).</summary>
public sealed class BookingPetMultiPickerModel
{
    public required IReadOnlyList<Pet> Pets { get; init; }
    public required IReadOnlyList<int> PetIds { get; init; }
    public string? ReturnUrl { get; init; }
    public string TitleEs { get; init; } = "¿Para qué mascotas?";
    public string TitleEn { get; init; } = "Which pets?";
    public string HintEs { get; init; } = "Activa una o varias (máx. 6).";
    public string HintEn { get; init; } = "Turn on one or more (max 6).";
    /// <summary>When set, other species are shown disabled.</summary>
    public IReadOnlyCollection<string>? AllowedSpecies { get; init; }
    public int Max { get; init; } = 6;
    public bool ShowPrimaryPetIdHidden { get; init; } = true;
    public int PrimaryPetId { get; init; }
}

/// <summary>Shared normalize for multi-pet booking flows.</summary>
public static class BookingPetSelection
{
    public static void Normalize(
        IReadOnlyList<Pet> pets,
        List<int> petIds,
        ref int petId,
        out List<Pet> selected,
        IEnumerable<string>? allowedSpecies = null,
        int max = 6)
    {
        petIds ??= new List<int>();
        var owned = pets.Select(p => p.Id).ToHashSet();
        var allowed = allowedSpecies?
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (petIds.Count == 0 && petId > 0 && owned.Contains(petId))
            petIds.Add(petId);

        IEnumerable<Pet> pool = pets;
        if (allowed is { Count: > 0 })
            pool = pets.Where(p => allowed.Contains(p.Species));

        var poolIds = pool.Select(p => p.Id).ToHashSet();
        var normalized = petIds.Where(poolIds.Contains).Distinct().Take(max).ToList();
        petIds.Clear();
        petIds.AddRange(normalized);

        var poolList = pool.ToList();
        if (petIds.Count == 0 && poolList.Count == 1)
            petIds.Add(poolList[0].Id);

        selected = pets.Where(p => petIds.Contains(p.Id)).ToList();
        petId = selected.FirstOrDefault()?.Id ?? 0;
    }

    public static string NamesSummary(IEnumerable<Pet> pets) =>
        string.Join(", ", pets.Select(p => $"{PetSpecies.Emoji(p.Species)} {p.Name}"));

    public static bool IsAllowed(Pet pet, IReadOnlyCollection<string>? allowedSpecies)
    {
        if (allowedSpecies is null || allowedSpecies.Count == 0) return true;
        return allowedSpecies.Contains(pet.Species, StringComparer.OrdinalIgnoreCase);
    }
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

public sealed class BookingTimeSlotsModel
{
    public required IReadOnlyList<string> Slots { get; init; }
    public string? Selected { get; init; }
    public IReadOnlyCollection<string> PastSlots { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> OccupiedSlots { get; init; } = Array.Empty<string>();
    /// <summary>Slots outside the business weekly open window for the selected day.</summary>
    public IReadOnlyCollection<string> OutsideHoursSlots { get; init; } = Array.Empty<string>();
    public string InputName { get; init; } = "Slot";
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
    public string? CustomerNotes { get; init; }
    public string TotalLabelEs { get; init; } = "Total";
    public string TotalLabelEn { get; init; } = "Total";
    public decimal Estimate { get; init; }
    public string? NoteEs { get; init; }
    public string? NoteEn { get; init; }

    public string PaymentHeadingEs { get; init; } = "Pago";
    public string PaymentHeadingEn { get; init; } = "Payment";
    public PaymentMethod? DefaultPayment { get; init; }
    public bool AcceptTerms { get; init; }

    public required string SubmitLabelEs { get; init; }
    public required string SubmitLabelEn { get; init; }
    public bool ShowSecureLockOnSubmit { get; init; }
    public bool ShowSecureFooter { get; init; } = true;

    public IReadOnlyList<BookingSummaryHidden> HiddenFields { get; init; } = [];
    public IReadOnlyList<BookingSummaryMultiHidden> MultiHiddenFields { get; init; } = [];

    /// <summary>Optional extra CSS classes on the root aside (e.g. booking-summary).</summary>
    public string? ExtraClass { get; init; }

    /// <summary>
    /// StickyContinue = Amazon-style bottom bar on configure screen.
    /// FullPage = dedicated checkout screen (payment / promo / confirm).
    /// </summary>
    public BookingCheckoutPresentation Presentation { get; init; } = BookingCheckoutPresentation.FullPage;

    /// <summary>URL to open full checkout (required for StickyContinue).</summary>
    public string? ContinueHref { get; init; }

    /// <summary>URL to go back to editing the booking (FullPage back / edit link).</summary>
    public string? EditHref { get; init; }

    public string ContinueLabelEs { get; init; } = "Continuar";
    public string ContinueLabelEn { get; init; } = "Continue";
}

public enum BookingCheckoutPresentation
{
    StickyContinue,
    FullPage
}

/// <summary>Build configure vs pay URLs while preserving the current query string.</summary>
public static class BookingCheckoutUrls
{
    public static string ForRequest(HttpRequest request, bool pay)
    {
        var pairs = new List<KeyValuePair<string, string?>>();
        foreach (var kv in request.Query)
        {
            if (string.Equals(kv.Key, "pay", StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var v in kv.Value)
                pairs.Add(new KeyValuePair<string, string?>(kv.Key, v));
        }
        if (pay)
            pairs.Add(new KeyValuePair<string, string?>("pay", "true"));

        return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(request.Path, pairs);
    }

    public static string ForPage(Microsoft.AspNetCore.Mvc.Rendering.ViewContext view, bool pay)
        => ForRequest(view.HttpContext.Request, pay);
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

        // Past dates (typed or soft-nav) clamp to today — never keep a past value in the flow.
        if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var parsed))
        {
            var day = parsed.Date < today ? today : parsed.Date;
            return ("", day.ToString("yyyy-MM-dd"));
        }

        return ("", null);
    }
}

/// <summary>Shared wall-clock slot helpers for Trainers / Walkers / Behavior / Booking / Vet.</summary>
public static class BookingTime
{
    public static readonly string[] DefaultSlots =
    {
        "9:00 AM", "10:00 AM", "11:00 AM", "1:00 PM", "2:00 PM", "3:00 PM", "4:00 PM", "5:00 PM"
    };

    public static HashSet<string> MarkPastSlots(IEnumerable<string> slots, DateTime day, BusinessMarket? market = null)
    {
        var zone = market ?? AppTimeZones.CurrentMarket;
        var past = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nowUtc = DateTime.UtcNow;
        foreach (var label in slots)
        {
            if (!AppTimeZones.TryParseSlotToTimeSpan(label, out var tod)) continue;
            var utc = AppTimeZones.LocalDateAndTimeToUtc(day.Date, tod, zone);
            if (utc <= nowUtc)
                past.Add(label);
        }
        return past;
    }

    public static bool IsSlotAvailable(
        string? slot,
        IEnumerable<string> catalog,
        IReadOnlySet<string> past,
        IReadOnlySet<string> occupied)
    {
        if (string.IsNullOrWhiteSpace(slot)) return false;
        if (!catalog.Contains(slot, StringComparer.OrdinalIgnoreCase)) return false;
        if (past.Contains(slot)) return false;
        if (occupied.Contains(slot)) return false;
        return true;
    }

    /// <summary>Maps legacy walker keys (ahora/manana9/tarde/noche) into DefaultSlots labels.</summary>
    public static string? MapLegacySlot(string? slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) return null;
        var key = slot.Trim();
        if (DefaultSlots.Contains(key, StringComparer.OrdinalIgnoreCase))
            return DefaultSlots.First(s => s.Equals(key, StringComparison.OrdinalIgnoreCase));

        return key.ToLowerInvariant() switch
        {
            "manana9" => "9:00 AM",
            "tarde" => "1:00 PM",
            "noche" => "5:00 PM",
            "ahora" => null,
            _ => null
        };
    }

    public static bool TryResolveStartUtc(string? slot, DateTime day, out DateTime startUtc, out string? error)
    {
        error = null;
        startUtc = default;
        if (!AppTimeZones.TryParseSlotToTimeSpan(slot, out var tod))
        {
            error = CatalogLocalizer.Loc("Elige un horario.", "Choose a time slot.");
            return false;
        }

        startUtc = AppTimeZones.LocalDateAndTimeToUtc(day.Date, tod);
        if (startUtc <= DateTime.UtcNow)
        {
            error = CatalogLocalizer.Loc(
                "No puedes elegir una fecha u hora en el pasado.",
                "You can't select a past date or time.");
            return false;
        }

        return true;
    }
}
