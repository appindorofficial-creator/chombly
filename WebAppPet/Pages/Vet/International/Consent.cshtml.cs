using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Consultations.GetIntlSchedule;
using WebAppPet.Application.Consultations.ScheduleIntlConsultation;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.International;

public class ConsentModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetIntlScheduleHandler _getSchedule;
    private readonly ScheduleIntlConsultationHandler _scheduleConsultation;

    public ConsentModel(AuthService auth, GetIntlScheduleHandler getSchedule, ScheduleIntlConsultationHandler scheduleConsultation)
    {
        _auth = auth;
        _getSchedule = getSchedule;
        _scheduleConsultation = scheduleConsultation;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? When { get; set; }

    [BindProperty] public bool AcceptScope { get; set; }
    [BindProperty] public string? Slot { get; set; }

    public Consultation? Consultation { get; set; }
    public GroomerProfile? Provider { get; set; }
    public ServiceCatalogItem? CatalogItem { get; set; }
    public bool UsingCare { get; set; }
    public decimal ConsultPrice { get; set; } = 30m;
    public string? ErrorMessage { get; set; }
    public List<string> TimeSlots { get; set; } = new();
    public HashSet<string> PastSlots { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool DayOpen { get; set; } = true;
    /// <summary>Home market of the signed-in client (CO/US) — drives US-only disclaimer copy.</summary>
    public string HomeCountryCode { get; set; } = MarketCountry.DefaultIso;
    public bool HomeIsUnitedStates => MarketCountry.IsUnitedStates(HomeCountryCode);

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/International/Consent?consultationId={ConsultationId}" });

        When ??= "hoy";
        var schedule = await _getSchedule.HandleAsync(new GetIntlScheduleQuery(userId, ConsultationId, When, Slot));
        if (schedule is null) return RedirectToPage("/Vet/International/Matches", new { consultationId = ConsultationId });

        Show(schedule);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Vet/International/Matches", new { consultationId = ConsultationId });

        var result = await _scheduleConsultation.HandleAsync(new ScheduleIntlConsultationCommand(
            userId, ConsultationId, When, Slot, AcceptScope,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString()));

        switch (result.Outcome)
        {
            case ScheduleIntlConsultationOutcome.NotFound:
                return RedirectToPage("/Vet/International/Matches", new { consultationId = ConsultationId });
            case ScheduleIntlConsultationOutcome.Scheduled:
                return RedirectToPage("/Vet/Virtual/Checkout", new { consultationId = ConsultationId });
            default:
                Show(result.Schedule!);
                return Page();
        }
    }

    private void Show(IntlSchedule schedule)
    {
        Consultation = schedule.Consultation;
        Provider = schedule.Provider;
        CatalogItem = schedule.CatalogItem;
        ConsultPrice = schedule.ConsultPrice;
        UsingCare = schedule.UsingCare;
        HomeCountryCode = schedule.HomeCountryCode;
        Slot = schedule.Slot;
        TimeSlots = schedule.TimeSlots;
        PastSlots = schedule.PastSlots;
        DayOpen = schedule.DayOpen;
    }
}
