using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.International;

public class ProvidersModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;
    private readonly ServiceCatalogService _catalog;
    private readonly ChomblyCareService _care;

    public ProvidersModel(
        AppDbContext db,
        AuthService auth,
        ConsultationFlowService flow,
        ServiceCatalogService catalog,
        ChomblyCareService care)
    {
        _db = db;
        _auth = auth;
        _flow = flow;
        _catalog = catalog;
        _care = care;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int CareBenefit { get; set; }

    [BindProperty]
    public int ProviderId { get; set; }

    [BindProperty]
    public string Slot { get; set; } = "10:30 AM";

    [BindProperty]
    public string When { get; set; } = "hoy";

    public Consultation? Consultation { get; set; }
    public ServiceCatalogItem? CatalogItem { get; set; }
    public List<GroomerProfile> Providers { get; set; } = new();
    public bool UsingCareBenefit { get; set; }
    public string? ErrorMessage { get; set; }

    public List<string> TimeSlots { get; } = new()
    {
        "9:00 AM", "10:00 AM", "10:30 AM", "11:30 AM", "1:00 PM", "2:00 PM", "3:00 PM", "4:00 PM", "5:00 PM"
    };

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/International/Providers?consultationId={ConsultationId}" });

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/Index");

        CatalogItem = await _catalog.GetAsync(ServiceCatalogCodes.VetIntl30);
        Consultation.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
        UsingCareBenefit = CareBenefit == 1 || Consultation.UsesCareBenefit;
        if (UsingCareBenefit && _auth.CurrentUserId is int uid)
        {
            if (!await _care.HasQuickConsultAvailableAsync(uid))
                return RedirectToPage("/Plans/ChomblyCare", new { consultationId = ConsultationId });
            Consultation.UsesCareBenefit = true;
        }
        await _flow.TouchAsync(Consultation);

        Providers = await _db.Groomers.AsNoTracking()
            .Include(g => g.Licenses)
            .Where(g => g.IsPublished && g.VetProviderKind == VetProviderKind.InternationalAdvisor)
            .OrderByDescending(g => g.Rating)
            .Take(30)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/Index");

        CatalogItem = await _catalog.GetAsync(ServiceCatalogCodes.VetIntl30);
        UsingCareBenefit = CareBenefit == 1 || Consultation.UsesCareBenefit;
        Providers = await _db.Groomers.AsNoTracking()
            .Include(g => g.Licenses)
            .Where(g => g.IsPublished && g.VetProviderKind == VetProviderKind.InternationalAdvisor)
            .OrderByDescending(g => g.Rating)
            .Take(30)
            .ToListAsync();

        if (ProviderId <= 0 || !Providers.Any(p => p.Id == ProviderId))
        {
            ErrorMessage = CatalogLocalizer.Loc("Selecciona un profesional.", "Select a professional.");
            return Page();
        }

        if (!AppTimeZones.TryParseSlotToTimeSpan(Slot, out var tod))
        {
            ErrorMessage = CatalogLocalizer.Loc("Elige un horario.", "Choose a time slot.");
            return Page();
        }

        var day = AppTimeZones.TodayLocalDate();
        if (string.Equals(When, "mañana", StringComparison.OrdinalIgnoreCase))
            day = day.AddDays(1);

        var provider = Providers.First(p => p.Id == ProviderId);
        Consultation.ProviderId = ProviderId;
        Consultation.ContextCountry = provider.LicenseCountry;
        Consultation.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
        Consultation.UsesCareBenefit = UsingCareBenefit;
        Consultation.ScheduledAt = AppTimeZones.LocalDateAndTimeToUtc(day, tod);
        Consultation.Status = ConsultationStatus.ProviderSelected;
        await _flow.TouchAsync(Consultation);

        return RedirectToPage("/Vet/Virtual/Checkout", new { consultationId = ConsultationId });
    }
}
