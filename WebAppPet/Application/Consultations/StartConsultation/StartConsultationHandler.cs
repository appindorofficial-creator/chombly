using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.StartConsultation;

/// <summary>
/// Opens a draft virtual consultation for the client's home market. Clients outside the
/// United States always get the guidance path.
/// </summary>
public class StartConsultationHandler
{
    private readonly AppDbContext _db;
    private readonly VetAuditService _audit;

    public StartConsultationHandler(AppDbContext db, VetAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<StartConsultationResult> HandleAsync(StartConsultationCommand command, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == command.ClientId, ct);
        var country = MarketCountry.ResolveForUser(user?.CountryCode, user?.City, user?.Latitude, user?.Longitude);

        var path = command.WantsLocal && MarketCountry.IsUnitedStates(country) ? ConsultationPath.Local : ConsultationPath.Intl;
        var catalogCode = path == ConsultationPath.Local ? ServiceCatalogCodes.VetLocal30 : ServiceCatalogCodes.VetIntl30;

        var consultation = new Consultation
        {
            ClientId = command.ClientId,
            Modality = VetModality.Virtual,
            Status = ConsultationStatus.Draft,
            PetUsState = UsStateFor(country, user),
            ContextCountry = country,
            ServiceCatalogCode = catalogCode,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        if (command.PetId is int petId && petId > 0
            && await _db.Pets.AsNoTracking().AnyAsync(p => p.Id == petId && p.OwnerId == command.ClientId, ct))
            consultation.PetId = petId;

        _db.Consultations.Add(consultation);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("modality_selected", command.ClientId, "Consultation", consultation.Id,
            new { next = path, catalogCode, petId = consultation.PetId }, ct);

        return new StartConsultationResult(consultation.Id, path);
    }

    /// <summary>"Other" in Colombia; in the US the state from the profile location, or NC.</summary>
    private static string UsStateFor(string country, AppUser? user)
    {
        if (country == "CO")
            return "Other";

        var resolved = GeoHelper.ResolveUsState(user?.City, user?.Latitude, user?.Longitude);
        return resolved is not null && !string.Equals(resolved, "Other", StringComparison.OrdinalIgnoreCase)
            ? resolved
            : "NC";
    }
}
