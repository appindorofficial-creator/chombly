using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Bookings.GetBookingConfirmation;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Identity;

namespace WebAppPet.Pages.Booking;

public class ConfirmModel : PageModel
{
    private readonly GetBookingConfirmationHandler _confirmation;
    private readonly AuthService _auth;

    public ConfirmModel(GetBookingConfirmationHandler confirmation, AuthService auth)
    {
        _confirmation = confirmation;
        _auth = auth;
    }

    public Appointment? Appointment { get; set; }
    public PaymentTransaction? Payment { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        var confirmation = await _confirmation.HandleAsync(new GetBookingConfirmationQuery(id, userId));
        if (confirmation == null)
            return RedirectToPage("/Appointments/Index");

        Appointment = confirmation.Appointment;
        Payment = confirmation.Payment;
        return Page();
    }
}
