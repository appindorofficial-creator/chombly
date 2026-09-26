using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.Shared;

/// <summary>A new, unsaved draft virtual consultation placed in the client's home market.</summary>
internal static class ConsultationDraft
{
    public static async Task<(Consultation Consultation, string HomeCountry)> NewAsync(AppDbContext db, int clientId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == clientId, ct);
        var country = MarketCountry.ResolveForUser(user?.CountryCode, user?.City, user?.Latitude, user?.Longitude);

        var consultation = new Consultation
        {
            ClientId = clientId,
            Modality = VetModality.Virtual,
            Status = ConsultationStatus.Draft,
            PetUsState = UsStateFor(country, user),
            ContextCountry = country,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        return (consultation, country);
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
