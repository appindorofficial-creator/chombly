using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Data;
using WebAppPet.Localization;

namespace WebAppPet.Application.Consultations.GetIntlHome;

/// <summary>
/// The international guidance landing: the client's language and the pet (from the link or
/// the consultation) the vet will be matched for.
/// </summary>
public class GetIntlHomeHandler
{
    private readonly AppDbContext _db;

    public GetIntlHomeHandler(AppDbContext db) => _db = db;

    public async Task<IntlHome> HandleAsync(GetIntlHomeQuery query, CancellationToken ct = default)
    {
        string? language = "es";
        if (query.UserId is int userId)
        {
            language = await _db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.PreferredLanguage)
                .FirstOrDefaultAsync(ct) ?? "es";
        }

        if (string.IsNullOrWhiteSpace(language))
            language = CatalogLocalizer.IsEnglish() ? "en" : "es";

        string? petName = null;
        string? petBreed = null;
        var petId = query.PetId;
        if (petId is null && query.ConsultationId is int consultationId && query.UserId is int clientId)
        {
            var consultation = await _db.OwnedConsultationAsync(clientId, consultationId, ct);
            petId = consultation?.PetId;
            if (!string.IsNullOrWhiteSpace(consultation?.PreferredBreed))
                petBreed = consultation.PreferredBreed;
        }

        if (petId is int id)
        {
            // Any pet id from the link is shown, not only the client's own pets.
            var pet = await _db.Pets.AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new { p.Name, p.Breed })
                .FirstOrDefaultAsync(ct);
            if (pet != null)
            {
                petName = pet.Name;
                if (string.IsNullOrWhiteSpace(petBreed))
                    petBreed = pet.Breed;
            }
        }

        return new IntlHome(language, petName, petBreed);
    }
}
