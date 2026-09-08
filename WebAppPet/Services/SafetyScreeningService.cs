using System.Text.Json;

namespace WebAppPet.Services;

public class SafetyScreeningService
{
    public record SafetyAnswers(
        bool BreathingTrouble,
        bool Seizures,
        bool Unconscious,
        bool SevereBleeding,
        bool ToxinIngestion,
        bool ExtremePain,
        bool CannotUrinate,
        string? Notes);

    public bool HasRedFlags(SafetyAnswers a)
        => a.BreathingTrouble || a.Seizures || a.Unconscious || a.SevereBleeding
           || a.ToxinIngestion || a.ExtremePain || a.CannotUrinate;

    public string Serialize(SafetyAnswers a) => JsonSerializer.Serialize(a);

    public SafetyAnswers? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<SafetyAnswers>(json); }
        catch { return null; }
    }
}
