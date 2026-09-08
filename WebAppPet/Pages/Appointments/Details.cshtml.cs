using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Appointments;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public DetailsModel(AppDbContext db, AuthService auth)
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
            .Include(a => a.Groomer)
            .Include(a => a.Service)
            .Include(a => a.Pet)
            .Include(a => a.Extras)
            .FirstOrDefaultAsync(a => a.Id == id && a.ClientId == userId);

        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        var appt = await _db.Appointments
            .Include(a => a.Groomer)
            .FirstOrDefaultAsync(a => a.Id == id && a.ClientId == userId);

        if (appt != null && appt.Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed)
        {
            appt.Status = AppointmentStatus.Cancelled;
            _db.Notifications.Add(new AppNotification
            {
                UserId = userId,
                Title = "Cita cancelada",
                Message = $"Cancelaste tu cita en {appt.Groomer.BusinessName}.",
                Type = "appointment"
            });
            await _db.SaveChangesAsync();
        }

        return RedirectToPage("./Index");
    }

    public string StatusLabel(AppointmentStatus s) => s switch
    {
        AppointmentStatus.Pending => "Pendiente",
        AppointmentStatus.Confirmed => "Confirmada",
        AppointmentStatus.Completed => "Completada",
        AppointmentStatus.Cancelled => "Cancelada",
        _ => s.ToString()
    };

    public string StatusBadge(AppointmentStatus s) => s switch
    {
        AppointmentStatus.Pending => "badge-orange",
        AppointmentStatus.Confirmed => "badge-green",
        AppointmentStatus.Completed => "badge-purple",
        _ => "badge-gray"
    };
}
