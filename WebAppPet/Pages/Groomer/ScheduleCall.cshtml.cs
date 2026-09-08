using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Groomer;

public class ScheduleCallModel : GroomerPageModel
{
    private readonly IEmailService _email;

    public ScheduleCallModel(AppDbContext db, AuthService auth, IEmailService email)
        : base(db, auth)
    {
        _email = email;
    }

    public List<DateTime> Days { get; set; } = new();
    public List<string> Slots { get; } = new() { "10:00 AM", "11:00 AM", "1:00 PM", "2:00 PM", "3:00 PM", "4:00 PM" };

    [BindProperty]
    public string? Day { get; set; }

    [BindProperty]
    public string Slot { get; set; } = "10:00 AM";

    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (await LoadGroomerAsync() is IActionResult r) return r;
        BuildDays();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await LoadGroomerAsync() is IActionResult r) return r;
        BuildDays();

        if (!DateTime.TryParse(Day, out var day))
        {
            ErrorMessage = "Elige un día.";
            return Page();
        }
        if (!DateTime.TryParse($"{day:yyyy-MM-dd} {Slot}", out var when))
        {
            ErrorMessage = "Horario inválido.";
            return Page();
        }

        Profile!.SupportCallAt = when;
        await Db.SaveChangesAsync();

        await _email.SendAsync(
            Profile.User?.Email ?? "",
            "Chombly: llamada agendada",
            $"<p>Hola {Profile.User?.FullName},</p><p>Tu llamada de soporte quedó para <strong>{when:ddd d MMM · h:mm tt}</strong>.</p><p>— Equipo Chombly</p>");

        // Avisar admin
        var adminEmail = "appindorofficial@gmail.com";
        await _email.SendAsync(adminEmail, $"Llamada soporte — {Profile.BusinessName}",
            $"<p>{Profile.BusinessName} agendó llamada: {when:g}</p><p>{Profile.User?.Email} · {Profile.Phone}</p>");

        Message = $"Llamada confirmada: {when:ddd d MMM · h:mm tt}";
        return Page();
    }

    private void BuildDays()
    {
        Days = Enumerable.Range(1, 7).Select(i => DateTime.Today.AddDays(i)).ToList();
        if (string.IsNullOrEmpty(Day)) Day = Days[0].ToString("yyyy-MM-dd");
    }
}
