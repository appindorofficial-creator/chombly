namespace WebAppPet.Application.Consultations.SelectIntlVet;

public sealed record SelectIntlVetCommand(int ClientId, int ConsultationId, int ProviderId);

public enum SelectIntlVetOutcome
{
    NotFound,
    /// <summary>The advisor is not an active, approved international advisor.</summary>
    ProviderUnavailable,
    Selected
}
