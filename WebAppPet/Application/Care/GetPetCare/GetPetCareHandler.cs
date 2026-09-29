using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Care.Shared;
using WebAppPet.Application.Reminders.Shared;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Care.GetPetCare;

public sealed record PetCareView(
    Pet Pet,
    CareSubscription? Subscription,
    int RemainingConsults,
    List<Consultation> RecentConsults,
    List<Appointment> Upcoming,
    ReminderSchedule? CheckupReminder);

/// <summary>
/// The pet's Care page: the family's membership, the pet's latest consultations, its next
/// appointments that are not cancelled and its active vaccines/checkup reminder.
/// Null when the pet is not the user's.
/// </summary>
public class GetPetCareHandler
{
    private const int RecentConsultsShown = 8;
    private const int UpcomingShown = 5;

    private readonly AppDbContext _db;
    private readonly ChomblyCareService _care;

    public GetPetCareHandler(AppDbContext db, ChomblyCareService care)
    {
        _db = db;
        _care = care;
    }

    public async Task<PetCareView?> HandleAsync(GetPetCareQuery query, CancellationToken ct = default)
    {
        var pet = await _db.Pets.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == query.PetId && p.OwnerId == query.UserId, ct);
        if (pet is null)
            return null;

        var subscription = await _care.GetActiveAsync(query.UserId, ct);

        var recentConsults = await _db.Consultations.AsNoTracking()
            .Include(c => c.Provider)
            .Where(c => c.ClientId == query.UserId && c.PetId == pet.Id)
            .OrderByDescending(c => c.UpdatedAt)
            .Take(RecentConsultsShown)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var upcoming = await _db.Appointments.AsNoTracking()
            .Include(a => a.Groomer)
            .Include(a => a.Service)
            .Where(a => a.ClientId == query.UserId && a.PetId == pet.Id &&
                        a.ScheduledAt >= now && a.Status != AppointmentStatus.Cancelled)
            .OrderBy(a => a.ScheduledAt)
            .Take(UpcomingShown)
            .ToListAsync(ct);

        return new PetCareView(
            pet,
            subscription,
            subscription is null ? 0 : _care.RemainingQuickConsults(subscription),
            recentConsults,
            upcoming,
            await _db.ActiveCheckupAsync(query.UserId, pet.Id, ct));
    }
}
