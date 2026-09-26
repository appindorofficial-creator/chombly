using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.ScreenConsultation;

/// <summary>
/// Saves the pet, symptoms, media and safety answers. Without warning signs the consultation
/// moves on to its path; with them the client must choose emergency or continue virtual.
/// </summary>
public class ScreenConsultationHandler
{
    private readonly AppDbContext _db;
    private readonly SafetyScreeningService _safety;
    private readonly VcprService _vcpr;
    private readonly VetAuditService _audit;
    private readonly ConsultationRouter _router;

    public ScreenConsultationHandler(
        AppDbContext db,
        SafetyScreeningService safety,
        VcprService vcpr,
        VetAuditService audit,
        ConsultationRouter router)
    {
        _db = db;
        _safety = safety;
        _vcpr = vcpr;
        _audit = audit;
        _router = router;
    }

    public async Task<ScreenConsultationResult> HandleAsync(ScreenConsultationCommand command, CancellationToken ct = default)
    {
        var context = await ScreeningContextLoader.LoadAsync(_db, command.ClientId, command.ConsultationId, command.Next, ct);
        if (context is null)
            return new ScreenConsultationResult(ScreenConsultationOutcome.NotFound, null, null, false, null);

        ScreenConsultationResult Stop(ScreenConsultationOutcome outcome, string? state) => new(outcome, context, state, false, null);

        if (command.PetId <= 0 || !context.Pets.Any(p => p.Id == command.PetId))
            return Stop(ScreenConsultationOutcome.Incomplete, command.PetUsState);

        string state;
        if (context.HomeCountryCode == "US")
        {
            if (string.IsNullOrWhiteSpace(command.PetUsState))
                return Stop(ScreenConsultationOutcome.Incomplete, command.PetUsState);
            state = command.PetUsState;
        }
        else
        {
            state = "Other";
        }

        if (command.Media1?.ValidationError is not null || command.Media2?.ValidationError is not null)
            return Stop(ScreenConsultationOutcome.MediaInvalid, state);

        var hasRedFlags = _safety.HasRedFlags(command.Answers);
        if (!hasRedFlags && !command.NoRedFlags)
            return Stop(ScreenConsultationOutcome.Incomplete, state);

        var consultation = context.Consultation;
        consultation.PetId = command.PetId;
        consultation.PetUsState = state.Trim().ToUpperInvariant();
        consultation.ContextCountry = context.HomeCountryCode;
        consultation.Symptoms = command.Symptoms?.Trim();
        if (command.Media1 is not null)
            consultation.MediaUrl1 = await command.Media1.SaveAsync(ct);
        if (command.Media2 is not null)
            consultation.MediaUrl2 = await command.Media2.SaveAsync(ct);

        consultation.SafetyAnswersJson = _safety.Serialize(command.Answers);
        consultation.HasRedFlags = hasRedFlags;
        consultation.HasActiveVcpr = await _vcpr.HasActiveAsync(command.PetId, consultation.PetUsState, ct);
        consultation.Status = ConsultationStatus.SafetyScreened;
        await _db.TouchAsync(consultation, ct);

        if (hasRedFlags)
        {
            await _audit.LogAsync("safety_flagged", command.ClientId, "Consultation", consultation.Id, command.Answers, ct);
            return new ScreenConsultationResult(ScreenConsultationOutcome.RedFlags, context, state, consultation.HasActiveVcpr, null);
        }

        var step = await _router.RouteAfterSafetyAsync(consultation, command.Next, context.HomeCountryCode, ct);
        return new ScreenConsultationResult(ScreenConsultationOutcome.Routed, context, state, consultation.HasActiveVcpr, step);
    }
}
