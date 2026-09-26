using WebAppPet.Application.Consultations.GetLocalVets;
using WebAppPet.Application.Consultations.Shared;

namespace WebAppPet.Application.Consultations.SelectLocalVet;

/// <param name="Slot">Time of day as shown, e.g. "10:30 AM".</param>
/// <param name="When">"mañana"/"tomorrow" for tomorrow; anything else is today.</param>
public sealed record SelectLocalVetCommand(int ClientId, int ConsultationId, int ProviderId, string? Slot, string? When);

/// <param name="NextStep">Where to go; null when the provider or slot was not valid.</param>
/// <param name="Options">The vets to show again when nothing was selected.</param>
public sealed record SelectLocalVetResult(ConsultationStep? NextStep, LocalVetOptions? Options);
