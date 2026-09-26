using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.ScreenConsultation;

/// <param name="PetUsState">State chosen in the form; only used when the client lives in the US.</param>
/// <param name="NoRedFlags">The client confirmed none of the warning signs apply.</param>
public sealed record ScreenConsultationCommand(
    int ClientId,
    int ConsultationId,
    string? Next,
    int PetId,
    string? PetUsState,
    string? Symptoms,
    ConsultationMedia? Media1,
    ConsultationMedia? Media2,
    SafetyScreeningService.SafetyAnswers Answers,
    bool NoRedFlags);

/// <summary>A photo or short video. It is only saved once the rest of the step is valid.</summary>
/// <param name="ValidationError">Localization key when the file is not accepted.</param>
public sealed record ConsultationMedia(string? ValidationError, Func<CancellationToken, Task<string>> SaveAsync);

public enum ScreenConsultationOutcome
{
    NotFound,

    /// <summary>Pet, state or confirmation missing; the form keeps Continue disabled, so no message.</summary>
    Incomplete,
    MediaInvalid,

    /// <summary>Saved with warning signs: the client chooses emergency or continue virtual.</summary>
    RedFlags,
    Routed
}

/// <param name="PetUsState">State as saved ("Other" outside the US).</param>
/// <param name="NextStep">Where to go when <paramref name="Outcome"/> is Routed.</param>
public sealed record ScreenConsultationResult(
    ScreenConsultationOutcome Outcome,
    ScreeningContext? Context,
    string? PetUsState,
    bool HasVcpr,
    ConsultationStep? NextStep);
