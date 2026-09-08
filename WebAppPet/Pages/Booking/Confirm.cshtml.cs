using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Booking;

public class ConfirmModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public ConfirmModel(AppDbContext db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    public Appointment? Appointment { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        Appointment = await _db.Appointments
            .Include(a => a.Groomer).ThenInclude(g => g.Category)
            .Include(a => a.Service)
            .Include(a => a.Pet)
            .Include(a => a.Extras)
            .FirstOrDefaultAsync(a => a.Id == id && a.ClientId == userId);

        if (Appointment == null)
            return RedirectToPage("/Appointments/Index");

        return Page();
    }
}
