using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.International;

public class ConsentModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;
    private readonly ServiceCatalogService _catalog;
    private readonly ConsentService _consent;
    private readonly AppDbContext _db;
    private readonly VetAuditService _audit;

    public ConsentModel(
        AuthService auth,
        ConsultationFlowService flow,
        ServiceCatalogService catalog,
        ConsentService consent,
        AppDbContext db,
        VetAuditService audit)
    {
        _auth = auth;
        _flow = flow;
        _catalog = catalog;
        _consent = consent;
        _db = db;
        _audit = audit;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int CareBenefit { get; set; }

    [BindProperty] public bool AcceptTerms { get; set; }
    [BindProperty] public bool AcceptScope { get; set; }
    [BindProperty] public bool AcceptMedia { get; set; }
    [BindProperty] public bool AcceptRenewal { get; set; }
    [BindProperty] public string Slot { get; set; } = "10:30 AM";
    [BindProperty] public string When { get; set; } = "hoy";

    public Consultation? Consultation { get; set; }
    public GroomerProfile? Provider { get; set; }
    public ServiceCatalogItem? CatalogItem { get; set; }
    public bool UsingCare { get; set; }
    public string? ErrorMessage { get; set; }

    public List<string> TimeSlots { get; } = new()
    {
        "9:00 AM", "10:00 AM", "10:30 AM", "1:00 PM", "2:00 PM", "3:00 PM", "5:00 PM"
    };

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/International/Consent?consultationId={ConsultationId}" });

        if (!await LoadAsync()) return RedirectToPage("/Vet/International/Home");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadAsync()) return RedirectToPage("/Vet/International/Home");

        if (!AcceptTerms || !AcceptScope)
        {
            ErrorMessage = CatalogLocalizer.Loc(
                "Marca las casillas de términos y alcance antes de continuar al pago.",
                "Check terms and scope boxes before continuing to payment.");
            return Page();
        }

        if (UsingCare && !AcceptRenewal && CareBenefit == 0)
        {
            // Only required when activating Care — if already using benefit, skip
        }

        if (!AppTimeZones.TryParseSlotToTimeSpan(Slot, out var tod))
        {
            ErrorMessage = CatalogLocalizer.Loc("Elige un horario.", "Choose a time.");
            return Page();
        }

        var day = AppTimeZones.TodayLocalDate();
        if (string.Equals(When, "mañana", StringComparison.OrdinalIgnoreCase))
            day = day.AddDays(1);

        Consultation!.ScheduledAt = AppTimeZones.LocalDateAndTimeToUtc(day, tod);
        Consultation.UsesCareBenefit = UsingCare;
        Consultation.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
        Consultation.Status = ConsultationStatus.ProviderSelected;
        await _flow.TouchAsync(Consultation);

        await _consent.SaveAsync(_auth.CurrentUserId!.Value, Consultation.Id, new[]
        {
            (ConsentService.DocTerms, AcceptTerms),
            (ConsentService.DocPrivacy, AcceptTerms),
            (ConsentService.DocIntlOrientation, AcceptScope),
            (ConsentService.DocMedia, AcceptMedia)
        }, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());

        await _audit.LogAsync("consent_accepted", _auth.CurrentUserId, "Consultation", ConsultationId);

        return RedirectToPage("/Vet/Virtual/Checkout", new { consultationId = ConsultationId });
    }

    private async Task<bool> LoadAsync()
    {
        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation?.ProviderId is null) return false;
        Provider = await _db.Groomers.AsNoTracking().FirstOrDefaultAsync(g => g.Id == Consultation.ProviderId);
        CatalogItem = await _catalog.GetAsync(ServiceCatalogCodes.VetIntl30);
        UsingCare = CareBenefit == 1 || Consultation.UsesCareBenefit;
        return CatalogItem != null;
    }
}
