using WebAppPet.Models;

namespace WebAppPet.Application.Consultations.GetConsultationServices;

public sealed record GetConsultationServicesQuery(int ClientId, int ConsultationId);

/// <param name="Items">Active services by price; the local teleconsult only for US clients.</param>
/// <param name="HasCare">The client has an active Chombly Care subscription.</param>
public sealed record ConsultationServices(
    Consultation Consultation,
    List<ServiceCatalogItem> Items,
    string HomeCountryCode,
    bool HasCare,
    int CareRemaining);
