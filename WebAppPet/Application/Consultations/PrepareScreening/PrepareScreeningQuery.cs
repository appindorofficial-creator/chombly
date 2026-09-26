using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.PrepareScreening;

/// <param name="Edit">Show the form again instead of resuming the emergency-or-virtual choice.</param>
public sealed record PrepareScreeningQuery(int ClientId, int ConsultationId, string? Next, bool Edit);

/// <param name="PetId">The consultation's pet, or the client's first pet.</param>
/// <param name="PetUsState">State preselected in the form ("Other" outside the US).</param>
/// <param name="StateFromLocation">The preselected state comes from the client's location.</param>
/// <param name="ShowPathChoice">Red flags were already saved: offer emergency or continue virtual.</param>
/// <param name="SavedAnswers">Saved answers to refill the form when the choice is shown.</param>
public sealed record ScreeningForm(
    ScreeningContext Context,
    int PetId,
    string PetUsState,
    bool StateFromLocation,
    bool HasVcpr,
    bool ShowPathChoice,
    SafetyScreeningService.SafetyAnswers? SavedAnswers);
