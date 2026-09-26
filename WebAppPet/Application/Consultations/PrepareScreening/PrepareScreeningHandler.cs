using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.PrepareScreening;

/// <summary>
/// Loads the pet and safety form. The consultation's country and US state are aligned with the
/// client's home market and location, and saved when they change.
/// </summary>
public class PrepareScreeningHandler
{
    private readonly AppDbContext _db;
    private readonly VcprService _vcpr;
    private readonly SafetyScreeningService _safety;

    public PrepareScreeningHandler(AppDbContext db, VcprService vcpr, SafetyScreeningService safety)
    {
        _db = db;
        _vcpr = vcpr;
        _safety = safety;
    }

    public async Task<ScreeningForm?> HandleAsync(PrepareScreeningQuery query, CancellationToken ct = default)
    {
        var context = await ScreeningContextLoader.LoadAsync(_db, query.ClientId, query.ConsultationId, query.Next, ct);
        if (context is null)
            return null;

        var consultation = context.Consultation;
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == query.ClientId, ct);
        var fromLocation = GeoHelper.ResolveUsState(user?.City, user?.Latitude, user?.Longitude);

        var (state, stateFromLocation, changed) = AlignLocation(consultation, context.HomeCountryCode, fromLocation);
        if (changed)
            await _db.TouchAsync(consultation, ct);

        var hasVcpr = consultation.PetId is int petId && await _vcpr.HasActiveAsync(petId, consultation.PetUsState, ct);

        var showChoice = !query.Edit
            && consultation.HasRedFlags
            && consultation.Status is ConsultationStatus.SafetyScreened or ConsultationStatus.EscalatedToEmergency;

        return new ScreeningForm(
            context,
            consultation.PetId ?? context.Pets.FirstOrDefault()?.Id ?? 0,
            state,
            stateFromLocation,
            hasVcpr,
            showChoice,
            showChoice ? _safety.Deserialize(consultation.SafetyAnswersJson) : null);
    }

    /// <summary>
    /// Colombia always uses "Other". In the US the state comes from the location for new
    /// consultations or the old NC default, otherwise from what was saved.
    /// </summary>
    private static (string State, bool FromLocation, bool Changed) AlignLocation(Consultation consultation, string homeCountry, string? fromLocation)
    {
        if (homeCountry == "CO")
        {
            var changedCo = !string.Equals(consultation.PetUsState, "Other", StringComparison.OrdinalIgnoreCase) ||
                            !string.Equals(consultation.ContextCountry, "CO", StringComparison.OrdinalIgnoreCase);
            if (changedCo)
            {
                consultation.PetUsState = "Other";
                consultation.ContextCountry = "CO";
            }

            return ("Other", !string.IsNullOrWhiteSpace(fromLocation), changedCo);
        }

        var storedState = string.IsNullOrWhiteSpace(consultation.PetUsState) ? null : consultation.PetUsState.Trim();
        var looksLikeLegacyNcDefault =
            string.Equals(storedState, "NC", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(consultation.ContextCountry, "US", StringComparison.OrdinalIgnoreCase);
        var locationIsState = !string.IsNullOrWhiteSpace(fromLocation) &&
                              !string.Equals(fromLocation, "Other", StringComparison.OrdinalIgnoreCase);

        string state;
        if (locationIsState && (consultation.PetId is null || looksLikeLegacyNcDefault))
            state = fromLocation!;
        else if (!string.IsNullOrWhiteSpace(storedState) &&
                 !string.Equals(storedState, "Other", StringComparison.OrdinalIgnoreCase) &&
                 !looksLikeLegacyNcDefault)
            state = storedState;
        else
            state = fromLocation is not null && !string.Equals(fromLocation, "Other", StringComparison.OrdinalIgnoreCase)
                ? fromLocation
                : "NC";

        var fromLocationUsed = !string.IsNullOrWhiteSpace(fromLocation) &&
                               string.Equals(state, fromLocation, StringComparison.OrdinalIgnoreCase);

        var changed = false;
        if (!string.Equals(consultation.PetUsState, state, StringComparison.OrdinalIgnoreCase))
        {
            consultation.PetUsState = state;
            changed = true;
        }
        if (!string.Equals(consultation.ContextCountry, "US", StringComparison.OrdinalIgnoreCase))
        {
            consultation.ContextCountry = "US";
            changed = true;
        }

        return (state, fromLocationUsed, changed);
    }
}
