using WebAppPet.Application.Consultations.Shared;

namespace WebAppPet.Application.Consultations.ChooseConsultationService;

/// <param name="UseCare">Use a Chombly Care quick consult (guidance) instead of the chosen service.</param>
public sealed record ChooseConsultationServiceCommand(int ClientId, int ConsultationId, string? ServiceCode, bool UseCare = false);

public enum ChooseConsultationServiceOutcome
{
    NotFound,

    /// <summary>The code is not a bookable service.</summary>
    Invalid,
    Moved
}

/// <param name="NextStep">Where to go when <paramref name="Outcome"/> is Moved.</param>
public sealed record ChooseConsultationServiceResult(ChooseConsultationServiceOutcome Outcome, ConsultationStep? NextStep = null);
