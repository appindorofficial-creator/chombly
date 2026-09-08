using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.Virtual;

public class ProvidersModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;
    private readonly ServiceCatalogService _catalog;

    public ProvidersModel(AppDbContext db, AuthService auth, ConsultationFlowService flow, ServiceCatalogService catalog)
    {
        _db = db;
        _auth = auth;
        _flow = flow;
        _catalog = catalog;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    [BindProperty]
    public int ProviderId { get; set; }

    [BindProperty]
    public string Slot { get; set; } = "10:30 AM";

    [BindProperty]
    public string When { get; set; } = "hoy";

    public Consultation? Consultation { get; set; }
    public ServiceCatalogItem? CatalogItem { get; set; }
    public List<GroomerProfile> Providers { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public List<string> TimeSlots { get; } = new()
    {
        "9:00 AM", "10:00 AM", "10:30 AM", "11:30 AM", "1:00 PM", "2:00 PM", "3:00 PM", "4:00 PM", "5:00 PM"
    };

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/Virtual/Providers?consultationId={ConsultationId}" });

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/Index");
        if (!Consultation.HasActiveVcpr)
            return RedirectToPage("/Vet/Virtual/Eligibility", new { consultationId = ConsultationId });

        Consultation.ServiceCatalogCode ??= ServiceCatalogCodes.VetLocal30;
        CatalogItem = await _catalog.GetAsync(ServiceCatalogCodes.VetLocal30);
        await LoadProvidersAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/Index");
        if (!Consultation.HasActiveVcpr)
            return RedirectToPage("/Vet/Virtual/Eligibility", new { consultationId = ConsultationId });

        CatalogItem = await _catalog.GetAsync(ServiceCatalogCodes.VetLocal30);
        await LoadProvidersAsync();

        if (ProviderId <= 0 || !Providers.Any(p => p.Id == ProviderId))
        {
            ErrorMessage = CatalogLocalizer.Loc("Selecciona un veterinario local.", "Select a local veterinarian.");
            return Page();
        }

        if (!AppTimeZones.TryParseSlotToTimeSpan(Slot, out var tod))
        {
            ErrorMessage = CatalogLocalizer.Loc("Elige un horario.", "Choose a time slot.");
            return Page();
        }

        var day = AppTimeZones.TodayLocalDate();
        if (string.Equals(When, "mañana", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(When, "tomorrow", StringComparison.OrdinalIgnoreCase))
            day = day.AddDays(1);

        Consultation.ProviderId = ProviderId;
        Consultation.ServiceCatalogCode = ServiceCatalogCodes.VetLocal30;
        Consultation.ScheduledAt = AppTimeZones.LocalDateAndTimeToUtc(day, tod);
        Consultation.Status = ConsultationStatus.ProviderSelected;
        await _flow.TouchAsync(Consultation);

        return RedirectToPage("/Vet/Virtual/Checkout", new { consultationId = ConsultationId });
    }

    private async Task LoadProvidersAsync()
    {
        var state = Consultation!.PetUsState;
        Providers = await _db.Groomers.AsNoTracking()
            .Include(g => g.Licenses)
            .Where(g => g.IsPublished &&
                        g.VetProviderKind == VetProviderKind.LocalVet &&
                        g.Licenses.Any(l => l.IsVerified && l.IsUsState && l.Jurisdiction == state))
            .OrderByDescending(g => g.Rating)
            .Take(30)
            .ToListAsync();

        if (Providers.Count == 0)
        {
            Providers = await _db.Groomers.AsNoTracking()
                .Include(g => g.Licenses)
                .Where(g => g.IsPublished && g.VetProviderKind == VetProviderKind.LocalVet)
                .OrderByDescending(g => g.Rating)
                .Take(30)
                .ToListAsync();
        }
    }
}
