namespace WebAppPet.Application.Consultations.ContinueVirtual;

public sealed record ContinueVirtualCommand(int ClientId, int ConsultationId, string? Next);
