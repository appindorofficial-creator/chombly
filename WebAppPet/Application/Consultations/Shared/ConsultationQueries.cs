using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Application.Consultations.Shared;

public static class ConsultationQueries
{
    /// <summary>The client's consultation with pet and provider, tracked for changes. Null when it is not theirs.</summary>
    public static Task<Consultation?> OwnedConsultationAsync(this AppDbContext db, int clientId, int consultationId, CancellationToken ct = default) =>
        db.Consultations
            .Include(c => c.Pet)
            .Include(c => c.Provider)
            .FirstOrDefaultAsync(c => c.Id == consultationId && c.ClientId == clientId, ct);

    /// <summary>Marks the consultation as updated now and saves all pending changes.</summary>
    public static Task TouchAsync(this AppDbContext db, Consultation consultation, CancellationToken ct = default)
    {
        consultation.UpdatedAt = DateTime.UtcNow;
        return db.SaveChangesAsync(ct);
    }
}
