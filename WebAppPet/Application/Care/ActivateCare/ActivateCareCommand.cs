namespace WebAppPet.Application.Care.ActivateCare;

/// <param name="ConsultationId">International consultation the family came from, if any; stored with the consent.</param>
public sealed record ActivateCareCommand(
    int UserId,
    int? ConsultationId,
    bool AcceptTerms,
    bool AcceptRenewal,
    string? IpAddress,
    string? UserAgent);
