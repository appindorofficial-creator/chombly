using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Reviews.CanReview;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Appointments;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly CanReviewHandler _canReview;

    public DetailsModel(AppDbContext db, AuthService auth, CanReviewHandler canReview)
    {
        _db = db;
        _auth = auth;
        _canReview = canReview;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string BackHref { get; private set; } = "/Appointments";

    public Appointment? Appointment { get; set; }
    public bool CanWriteReview { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        Appointment = await _db.Appointments
            .Include(a => a.Groomer)!.ThenInclude(g => g.Category)
            .Include(a => a.Service)
            .Include(a => a.Pet)
            .Include(a => a.Extras)
            .FirstOrDefaultAsync(a => a.Id == id && a.ClientId == userId);

        if (Appointment != null)
            CanWriteReview = (await _canReview.HandleAsync(
                new CanReviewQuery(userId, Appointment.GroomerId, Appointment.Id))).CanReview;

        BackHref = ResolveBackHref();
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
                Title = CatalogLocalizer.Loc("Cita cancelada", "Appointment cancelled"),
                Message = CatalogLocalizer.Loc(
                    $"Cancelaste tu cita en {appt.Groomer.BusinessName}.",
                    $"You cancelled your appointment at {appt.Groomer.BusinessName}."),
                Type = "appointment"
            });
            await _db.SaveChangesAsync();
        }

        return RedirectToPage("./Index");
    }

    public string StatusLabel(AppointmentStatus s) => s switch
    {
        AppointmentStatus.Pending => CatalogLocalizer.Loc("Pendiente", "Pending"),
        AppointmentStatus.Confirmed => CatalogLocalizer.Loc("Confirmada", "Confirmed"),
        AppointmentStatus.Completed => CatalogLocalizer.Loc("Completada", "Completed"),
        AppointmentStatus.Cancelled => CatalogLocalizer.Loc("Cancelada", "Cancelled"),
        _ => s.ToString()
    };

    public string StatusBadge(AppointmentStatus s) => s switch
    {
        AppointmentStatus.Pending => "badge-orange",
        AppointmentStatus.Confirmed => "badge-green",
        AppointmentStatus.Completed => "badge-purple",
        _ => "badge-gray"
    };

    private string ResolveBackHref()
    {
        if (TryLocalPath(ReturnUrl, out var fromQuery))
            return fromQuery;

        var referer = Request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var uri)
            && string.Equals(uri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase)
            && TryLocalPath(uri.PathAndQuery, out var fromReferer))
        {
            return fromReferer;
        }

        return Url.Page("./Index") ?? "/Appointments";
    }

    private bool TryLocalPath(string? candidate, out string path)
    {
        path = "/Appointments";
        if (string.IsNullOrWhiteSpace(candidate)) return false;

        var value = candidate.Trim();
        if (!value.StartsWith('/') || value.StartsWith("//", StringComparison.Ordinal))
            return false;
        if (!Url.IsLocalUrl(value))
            return false;

        var pathOnly = value.Split('?', 2)[0];
        // Never bounce Details ↔ Chat; both should exit toward the appointments list.
        if (pathOnly.Contains("/Appointments/Details", StringComparison.OrdinalIgnoreCase))
            return false;
        if (pathOnly.Equals("/Chat", StringComparison.OrdinalIgnoreCase)
            || pathOnly.Equals("/Chat/Index", StringComparison.OrdinalIgnoreCase)
            || pathOnly.StartsWith("/Chat/", StringComparison.OrdinalIgnoreCase))
            return false;

        path = value;
        return true;
    }
}
