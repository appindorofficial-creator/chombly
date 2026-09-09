using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Appointments;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly IStringLocalizer<SharedResource> _L;

    public IndexModel(AppDbContext db, AuthService auth, IStringLocalizer<SharedResource> L)
    {
        _db = db;
        _auth = auth;
        _L = L;
    }

    [BindProperty(SupportsGet = true)]
    public string Tab { get; set; } = "upcoming";

    public bool IsGuest { get; set; }
    public List<Appointment> Items { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
        {
            IsGuest = true;
            return Page();
        }

        var query = _db.Appointments
            .Include(a => a.Groomer)!.ThenInclude(g => g.Category)
            .Include(a => a.Service)
            .Include(a => a.Pet)
            .Where(a => a.ClientId == userId);

        if (Tab == "history")
        {
            query = query.Where(a => a.Status == AppointmentStatus.Completed || a.Status == AppointmentStatus.Cancelled
                || a.ScheduledAt < DateTime.Now);
            Items = await query.OrderByDescending(a => a.ScheduledAt).ToListAsync();
        }
        else
        {
            query = query.Where(a => a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed)
                .Where(a => a.ScheduledAt >= DateTime.Now.Date);
            Items = await query.OrderBy(a => a.ScheduledAt).ToListAsync();
        }

        return Page();
    }

    public string StatusLabel(AppointmentStatus s) => s switch
    {
        AppointmentStatus.Pending => _L["Appt_Status_Pending"].Value,
        AppointmentStatus.Confirmed => _L["Appt_Status_Confirmed"].Value,
        AppointmentStatus.Completed => _L["Appt_Status_Completed"].Value,
        AppointmentStatus.Cancelled => _L["Appt_Status_Cancelled"].Value,
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
