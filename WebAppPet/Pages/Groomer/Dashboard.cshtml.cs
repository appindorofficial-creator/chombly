using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Groomer;

public class DashboardModel : GroomerPageModel
{
    public DashboardModel(AppDbContext db, AuthService auth) : base(db, auth) { }

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

        var today = DateTime.Today;
        var completedToday = await Db.Appointments
            .Where(a => a.GroomerId == Profile.Id
                && a.Status == AppointmentStatus.Completed
                && a.ScheduledAt >= today && a.ScheduledAt < today.AddDays(1))
            .ToListAsync();

        TodayIncome = completedToday.Sum(a => a.TotalPrice);
        TodayCompleted = completedToday.Count;

        PendingCount = await Db.Appointments
            .CountAsync(a => a.GroomerId == Profile.Id && a.Status == AppointmentStatus.Pending);

        UpcomingCount = await Db.Appointments
            .CountAsync(a => a.GroomerId == Profile.Id
                && a.Status == AppointmentStatus.Confirmed
                && a.ScheduledAt >= DateTime.Now);

        Upcoming = await Db.Appointments
            .Include(a => a.Pet)
            .Include(a => a.Service)
            .Include(a => a.Client)
            .Where(a => a.GroomerId == Profile.Id
                && (a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.Pending)
                && a.ScheduledAt >= DateTime.Now.AddHours(-2))
            .OrderBy(a => a.ScheduledAt)
            .Take(6)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostResubmitAsync()
    {
        if (await LoadGroomerAsync() is IActionResult redirect) return redirect;
        var g = Profile!;
        if (g.PublishStatus is BusinessPublishStatus.Rejected or BusinessPublishStatus.Draft)
        {
            g.PublishStatus = BusinessPublishStatus.PendingReview;
            g.IsActive = false;
            g.IsVerified = false;
            var admins = await Db.Users.Where(u => u.Role == UserRole.Admin).Select(u => u.Id).ToListAsync();
            foreach (var adminId in admins)
            {
                Db.Notifications.Add(new AppNotification
                {
                    UserId = adminId,
                    Title = "Negocio reenviado a revisión",
                    Message = $"{g.BusinessName} vuelve a solicitar publicación.",
                    Type = "business"
                });
            }
            await Db.SaveChangesAsync();
            TempData["Flash"] = "Solicitud reenviada. Te avisaremos al publicar.";
        }
        return RedirectToPage();
    }
}
