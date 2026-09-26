using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Data;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.FindIntlVet;

/// <summary>Puts the consultation (or a new draft) on international guidance and sends the client to the vet matches.</summary>
public class FindIntlVetHandler
{
    private readonly AppDbContext _db;
    private readonly ConsultationRouter _router;
    private readonly VetAuditService _audit;

    public FindIntlVetHandler(AppDbContext db, ConsultationRouter router, VetAuditService audit)
    {
        _db = db;
        _router = router;
        _audit = audit;
    }

    public async Task<FindIntlVetResult> HandleAsync(FindIntlVetCommand command, CancellationToken ct = default)
    {
        var consultation = command.ConsultationId is int id
            ? await _db.OwnedConsultationAsync(command.ClientId, id, ct)
            : null;
        if (consultation is null)
        {
            (consultation, _) = await ConsultationDraft.NewAsync(_db, command.ClientId, ct);
            _db.Consultations.Add(consultation);
        }

        // The pet id from the link is not checked against the client's own pets.
        if (command.PetId is int petId && petId > 0 && consultation.PetId is null)
            consultation.PetId = petId;

        await _router.UseGuidanceAsync(consultation, ct);
        await _audit.LogAsync("intl_landing_view", command.ClientId, "Consultation", consultation.Id, ct: ct);

        var next = consultation.PetId is null || string.IsNullOrWhiteSpace(consultation.PetUsState)
            ? ConsultationStep.Pet
            : ConsultationStep.IntlMatches;
        return new FindIntlVetResult(consultation.Id, next);
    }
}
