using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Application.Pets.GetPetHistory;

/// <summary>Completed appointments and the owner's consultations for one pet. Null when the pet is not theirs.</summary>
public class GetPetHistoryHandler
{
    private readonly AppDbContext _db;

    public GetPetHistoryHandler(AppDbContext db) => _db = db;

    public async Task<PetHistory?> HandleAsync(GetPetHistoryQuery query, CancellationToken ct = default)
    {
        var pet = await _db.Pets.FirstOrDefaultAsync(p => p.Id == query.PetId && p.OwnerId == query.OwnerId, ct);
        if (pet is null)
            return null;

        var appointments = await _db.Appointments
            .Include(a => a.Groomer)
            .Include(a => a.Service)
            .Where(a => a.PetId == query.PetId && a.Status == AppointmentStatus.Completed)
            .OrderByDescending(a => a.ScheduledAt)
            .ToListAsync(ct);

        var consultations = await _db.Consultations
            .AsNoTracking()
            .Include(c => c.Provider)
            .Where(c => c.ClientId == query.OwnerId && c.PetId == query.PetId)
            .OrderByDescending(c => c.ScheduledAt ?? c.UpdatedAt)
            .ToListAsync(ct);

        return new PetHistory(pet, appointments, consultations);
    }
}
