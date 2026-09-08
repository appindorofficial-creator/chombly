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

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        Pet = await _db.Pets.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == userId);
        if (Pet == null)
            return RedirectToPage("./Index");

        Items = await _db.Appointments
            .Include(a => a.Groomer)
            .Include(a => a.Service)
            .Where(a => a.PetId == id && a.Status == AppointmentStatus.Completed)
            .OrderByDescending(a => a.ScheduledAt)
            .ToListAsync();

        return Page();
    }
}
