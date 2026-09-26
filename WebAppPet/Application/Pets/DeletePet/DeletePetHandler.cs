using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;

namespace WebAppPet.Application.Pets.DeletePet;

/// <summary>
/// Deletes one of the owner's pets when it has no appointments. Consultations and behavior
/// cases keep their history without the pet; its reminders are removed.
/// </summary>
public class DeletePetHandler
{
    private readonly AppDbContext _db;

    public DeletePetHandler(AppDbContext db) => _db = db;

    public async Task<DeletePetResult> HandleAsync(DeletePetCommand command, CancellationToken ct = default)
    {
        var pet = await _db.Pets.FirstOrDefaultAsync(p => p.Id == command.PetId && p.OwnerId == command.OwnerId, ct);
        if (pet is null)
            return DeletePetResult.NotFound;

        if (await _db.Appointments.AnyAsync(a => a.PetId == command.PetId, ct))
            return DeletePetResult.HasAppointments;

        // SQL Server uses NO ACTION on these FKs, so they cannot SET NULL on delete.
        await _db.Consultations.Where(c => c.PetId == command.PetId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.PetId, (int?)null), ct);
        await _db.BehaviorCases.Where(b => b.PetId == command.PetId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.PetId, (int?)null), ct);
        await _db.ReminderSchedules.Where(r => r.PetId == command.PetId)
            .ExecuteDeleteAsync(ct);

        _db.Pets.Remove(pet);
        await _db.SaveChangesAsync(ct);
        return DeletePetResult.Deleted;
    }
}
