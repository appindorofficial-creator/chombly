using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Models;

namespace WebAppPet.Application.Consultations.GetIntlMatches;

/// <param name="Lang">Language filter (es, en, …); the client's language when empty.</param>
public sealed record GetIntlMatchesQuery(int ClientId, int ConsultationId, string? Lang);

/// <param name="Redirect">Set when the list cannot be shown yet (not the client's consultation, or no pet).</param>
/// <param name="ActiveLang">The language filter applied.</param>
public sealed record IntlMatches(
    ConsultationStep? Redirect,
    List<IntlMatch> Matches,
    List<LanguageChip> LanguageChips,
    decimal ConsultPrice,
    bool HasCareBenefit,
    int CareRemaining,
    string ActiveLang)
{
    public static IntlMatches RedirectTo(ConsultationStep step) => new(step, [], [], 0, false, 0, "");
}

public sealed record IntlMatch(
    GroomerProfile Provider,
    string Why,
    string CountryName,
    string LanguagesDisplay,
    bool SpeaksUserLang,
    int Score,
    decimal Price);

public sealed record LanguageChip(string Code, string Label, int Count, bool Active);
