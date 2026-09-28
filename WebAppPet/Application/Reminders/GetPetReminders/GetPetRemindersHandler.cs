using WebAppPet.Application.Reminders.Shared;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Application.Reminders.GetPetReminders;

public sealed record PetRemindersView(Pet Pet, List<ReminderSchedule> Schedules);

/// <summary>The pet and its active reminders, soonest first. Null when the pet is not the user's.</summary>
public class GetPetRemindersHandler
{
    private readonly AppDbContext _db;

    public GetPetRemindersHandler(AppDbContext db) => _db = db;

    public async Task<PetRemindersView?> HandleAsync(GetPetRemindersQuery query, CancellationToken ct = default)
    {
        var pet = await _db.OwnedPetAsync(query.UserId, query.PetId, ct);
        if (pet is null)
            return null;

        return new PetRemindersView(pet, await _db.ActiveForPetAsync(query.UserId, query.PetId, ct));
    }
}
