using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Pets;

public class HistoryModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public HistoryModel(AppDbContext db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    public Pet? Pet { get; set; }
    public List<Appointment> Items { get; set; } = new();
    public List<Consultation> Consults { get; set; } = new();
    public string BackHref { get; private set; } = "/Pets";

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        Pet = await _db.Pets.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == userId);
        if (Pet == null)
            return RedirectToPage("./Index");

        BackHref = !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
            ? ReturnUrl!
            : Url.Page("./Index") ?? "/Pets";

        Items = await _db.Appointments
            .Include(a => a.Groomer)
            .Include(a => a.Service)
            .Where(a => a.PetId == id && a.Status == AppointmentStatus.Completed)
            .OrderByDescending(a => a.ScheduledAt)
            .ToListAsync();

        Consults = await _db.Consultations
            .AsNoTracking()
            .Include(c => c.Provider)
            .Where(c => c.ClientId == userId && c.PetId == id)
            .OrderByDescending(c => c.ScheduledAt ?? c.UpdatedAt)
            .ToListAsync();

        return Page();
    }
}
