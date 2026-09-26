using WebAppPet.Models;

namespace WebAppPet.Application.Consultations.GetConsultationSummary;

public sealed record GetConsultationSummaryQuery(int ClientId, int ConsultationId);

/// <param name="CatalogItem">Null when the consultation's service is no longer active.</param>
public sealed record ConsultationSummary(Consultation Consultation, ServiceCatalogItem? CatalogItem, string HomeCountryCode);
