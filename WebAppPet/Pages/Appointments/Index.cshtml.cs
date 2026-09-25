using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using WebAppPet.Application.Bookings.GetBookings;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Appointments;

public class IndexModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetClientBookingsHandler _getBookings;
    private readonly IStringLocalizer<SharedResource> _L;

    public IndexModel(AuthService auth, GetClientBookingsHandler getBookings, IStringLocalizer<SharedResource> L)
    {
        _auth = auth;
        _getBookings = getBookings;
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

        Items = await _getBookings.HandleAsync(new GetClientBookingsQuery(userId, Tab == "history", DateTime.Now));
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
