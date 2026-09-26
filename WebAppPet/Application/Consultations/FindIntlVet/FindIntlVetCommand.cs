using WebAppPet.Application.Consultations.Shared;

namespace WebAppPet.Application.Consultations.FindIntlVet;

/// <param name="ConsultationId">The consultation to continue; a new draft is opened when missing or not the client's.</param>
public sealed record FindIntlVetCommand(int ClientId, int? ConsultationId, int? PetId);

/// <param name="Next">The pet step while the pet or location is missing, otherwise the vet matches.</param>
public sealed record FindIntlVetResult(int ConsultationId, ConsultationStep Next);
