using WebAppPet.Models;

namespace WebAppPet.Application.Consultations.GetIntlSchedule;

/// <param name="When">"hoy" or "mañana"; any other value shows today's slots.</param>
/// <param name="Slot">The slot the client picked; dropped when it is not offered or already past.</param>
public sealed record GetIntlScheduleQuery(int ClientId, int ConsultationId, string? When, string? Slot);

/// <param name="HomeCountryCode">The client's home market; drives the US-only disclaimer.</param>
public sealed record IntlSchedule(
    Consultation Consultation,
    GroomerProfile Provider,
    ServiceCatalogItem CatalogItem,
    decimal ConsultPrice,
    bool UsingCare,
    string HomeCountryCode,
    string? Slot,
    List<string> TimeSlots,
    HashSet<string> PastSlots,
    bool DayOpen);
