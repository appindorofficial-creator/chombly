using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Pets;

public class CareModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly ChomblyCareService _care;

    public CareModel(AppDbContext db, AuthService auth, ChomblyCareService care)
    {
        _db = db;
        _auth = auth;
        _care = care;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public Pet? Pet { get; set; }
    public CareSubscription? Subscription { get; set; }
    public int RemainingConsults { get; set; }
    public List<Consultation> RecentConsults { get; set; } = new();
    public List<Appointment> Upcoming { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Pets/Care/{Id}" });

        Pet = await _db.Pets.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == Id && p.OwnerId == _auth.CurrentUserId);
        if (Pet is null) return RedirectToPage("/Pets/Index");

        Subscription = await _care.GetActiveAsync(_auth.CurrentUserId.Value);
        if (Subscription != null)
            RemainingConsults = _care.RemainingQuickConsults(Subscription);

        RecentConsults = await _db.Consultations.AsNoTracking()
            .Include(c => c.Provider)
            .Where(c => c.ClientId == _auth.CurrentUserId && c.PetId == Id)
            .OrderByDescending(c => c.UpdatedAt)
            .Take(8)
            .ToListAsync();

        var now = DateTime.UtcNow;
        Upcoming = await _db.Appointments.AsNoTracking()
            .Include(a => a.Groomer)
            .Include(a => a.Service)
            .Where(a => a.ClientId == _auth.CurrentUserId && a.PetId == Id &&
                        a.ScheduledAt >= now && a.Status != AppointmentStatus.Cancelled)
            .OrderBy(a => a.ScheduledAt)
            .Take(5)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostRemindAsync()
    {
        if (_auth.CurrentUserId is null) return RedirectToPage("/Account/Login");

        Pet = await _db.Pets.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == Id && p.OwnerId == _auth.CurrentUserId);
        if (Pet is null) return RedirectToPage("/Pets/Index");

        _db.Notifications.Add(new AppNotification
        {
            UserId = _auth.CurrentUserId.Value,
            Title = CatalogLocalizer.Loc("Recordatorio de cuidado", "Care reminder"),
            Message = CatalogLocalizer.Loc(
                $"Revisa vacunas y chequeo de {Pet.Name}.",
                $"Review vaccines and checkup for {Pet.Name}."),
            Type = "care-reminder",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        return RedirectToPage(new { id = Id });
    }
}
