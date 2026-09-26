using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Models;

namespace WebAppPet.Application.Consultations.GetLocalVets;

public sealed record GetLocalVetsQuery(int ClientId, int ConsultationId);

/// <param name="Redirect">Set when the client cannot pick a local vet yet (or at all).</param>
/// <param name="Providers">Verified vets licensed in the pet's state, or any local vet when none is.</param>
public sealed record LocalVetOptions(
    ConsultationStep? Redirect,
    Consultation? Consultation,
    ServiceCatalogItem? CatalogItem,
    List<GroomerProfile> Providers)
{
    public static LocalVetOptions RedirectTo(ConsultationStep step) => new(step, null, null, []);
}
