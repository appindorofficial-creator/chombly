using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Businesses.ResubmitBusiness;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Identity;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Localization;

namespace WebAppPet.Pages.Groomer;

public class DashboardModel : GroomerPageModel
{
    private readonly ResubmitBusinessHandler _resubmit;

    public DashboardModel(AppDbContext db, AuthService auth, ResubmitBusinessHandler resubmit) : base(db, auth)
    {
        _resubmit = resubmit;
    }

    public decimal TodayIncome { get; set; }
    public int TodayCompleted { get; set; }
    public int UpcomingCount { get; set; }
    public int PendingCount { get; set; }
    public List<Appointment> Upcoming { get; set; } = new();
    public bool JustRegistered { get; set; }
    public int OpenWeekDays { get; set; }
    public int ServiceCount { get; set; }
    public string? Flash { get; set; }

    public async Task<IActionResult> OnGetAsync(int? registered)
    {
        if (await LoadGroomerAsync() is IActionResult redirect) return redirect;

        Profile = await Db.Groomers.Include(g => g.User).Include(g => g.Category).FirstAsync(g => g.Id == Profile!.Id);
        JustRegistered = registered == 1;
        Flash = TempData["Flash"] as string;
        OpenWeekDays = await Db.WeeklyHours.CountAsync(h => h.GroomerId == Profile.Id && h.IsOpen);
        ServiceCount = await Db.Services.CountAsync(s => s.GroomerId == Profile.Id);

        var todayLocal = AppTimeZones.TodayLocalDate();
        var todayStartUtc = AppTimeZones.LocalDateAndTimeToUtc(todayLocal, TimeSpan.Zero);
        var tomorrowStartUtc = AppTimeZones.LocalDateAndTimeToUtc(todayLocal.AddDays(1), TimeSpan.Zero);
        var completedToday = await Db.Appointments
            .Where(a => a.GroomerId == Profile.Id
                && a.Status == AppointmentStatus.Completed
                && a.ScheduledAt >= todayStartUtc && a.ScheduledAt < tomorrowStartUtc)
            .ToListAsync();

        TodayIncome = completedToday.Sum(a => a.TotalPrice);
        TodayCompleted = completedToday.Count;

        PendingCount = await Db.Appointments
            .CountAsync(a => a.GroomerId == Profile.Id && a.Status == AppointmentStatus.Pending);

        var nowUtc = DateTime.UtcNow;
        UpcomingCount = await Db.Appointments
            .CountAsync(a => a.GroomerId == Profile.Id
                && a.Status == AppointmentStatus.Confirmed
                && a.ScheduledAt >= nowUtc);

        Upcoming = await Db.Appointments
            .Include(a => a.Pet)
            .Include(a => a.Service)
            .Include(a => a.Client)
            .Where(a => a.GroomerId == Profile.Id
                && (a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.Pending)
                && a.ScheduledAt >= nowUtc.AddHours(-2))
            .OrderBy(a => a.ScheduledAt)
            .Take(6)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostResubmitAsync()
    {
        if (await LoadGroomerAsync() is IActionResult redirect) return redirect;
        if (await _resubmit.HandleAsync(new ResubmitBusinessCommand(Profile!.Id)))
        {
            TempData["Flash"] = CatalogLocalizer.Loc(
                "Solicitud reenviada. Te avisaremos al publicar.",
                "Request resubmitted. We'll notify you when you're published.");
        }
        return RedirectToPage();
    }
}
