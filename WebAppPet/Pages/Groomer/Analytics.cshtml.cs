using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Groomer;

public class AnalyticsModel : GroomerPageModel
{
    public AnalyticsModel(AppDbContext db, AuthService auth) : base(db, auth) { }

    public decimal MonthIncome { get; set; }
    public int Completed { get; set; }
    public int NewRequests { get; set; }
    public int UniqueClients { get; set; }
    public int Cancelled { get; set; }
    public List<(string Name, int Count, int Percent)> TopServices { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (await LoadGroomerAsync() is IActionResult redirect) return redirect;

        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        var monthAppts = await Db.Appointments
            .Include(a => a.Service)
            .Where(a => a.GroomerId == Profile!.Id && a.ScheduledAt >= monthStart.AddDays(-60))
            .ToListAsync();

        var thisMonth = monthAppts.Where(a => a.ScheduledAt >= monthStart).ToList();

        MonthIncome = thisMonth.Where(a => a.Status == AppointmentStatus.Completed).Sum(a => a.TotalPrice);
        Completed = thisMonth.Count(a => a.Status == AppointmentStatus.Completed);
        NewRequests = thisMonth.Count(a => a.Status == AppointmentStatus.Pending);
        Cancelled = thisMonth.Count(a => a.Status == AppointmentStatus.Cancelled);
        UniqueClients = monthAppts.Select(a => a.ClientId).Distinct().Count();

        var grouped = monthAppts
            .GroupBy(a => a.Service.Name)
            .Select(g => (g.Key, g.Count()))
            .OrderByDescending(x => x.Item2)
            .Take(5)
            .ToList();

        var max = grouped.Count > 0 ? grouped.Max(x => x.Item2) : 1;
        TopServices = grouped
            .Select(x => (x.Key, x.Item2, (int)Math.Round(100.0 * x.Item2 / max)))
            .ToList();

        return Page();
    }
}
