using WebAppPet.Models;

namespace WebAppPet.Application.Consultations.GetConsultationCheckout;

/// <param name="LogStarted">Record "checkout_started" in the vet audit log.</param>
public sealed record GetConsultationCheckoutQuery(int ClientId, int ConsultationId, bool LogStarted = false);

/// <param name="CatalogPrice">The provider's starting price, or the catalog price when it has none.</param>
/// <param name="CareRemaining">Chombly Care quick consults left this period.</param>
/// <param name="Payments">The client's saved cards, default first.</param>
public sealed record CheckoutDetails(
    Consultation Consultation,
    ServiceCatalogItem CatalogItem,
    GroomerProfile? Provider,
    decimal CatalogPrice,
    int CareRemaining,
    string HomeCountryCode,
    List<PaymentMethod> Payments,
    PaymentMethod? DefaultPayment)
{
    public bool HasCareAvailable => CareRemaining > 0;
}
