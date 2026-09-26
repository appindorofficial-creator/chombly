namespace WebAppPet.Application.Consultations.StartConsultation;

/// <param name="WantsLocal">The client asked for the US state-licensed teleconsult instead of guidance.</param>
/// <param name="PetId">Pet preselected from the pet card; ignored when it is not the client's.</param>
public sealed record StartConsultationCommand(int ClientId, bool WantsLocal, int? PetId);

/// <param name="Path">"local" or "intl", passed on to the pet step.</param>
public sealed record StartConsultationResult(int ConsultationId, string Path);
