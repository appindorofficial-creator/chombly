using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.Virtual;

public class ServiceModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;
    private readonly ServiceCatalogService _catalog;
    private readonly ChomblyCareService _care;

    public ServiceModel(
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

    [BindProperty]
    public string? ServiceCode { get; set; }

    public Consultation? Consultation { get; set; }
    public List<ServiceCatalogItem> Items { get; set; } = new();
    public bool HasCare { get; set; }
    public int CareRemaining { get; set; }
    public string? ErrorMessage { get; set; }
    public string HomeCountryCode { get; set; } = MarketCountry.DefaultIso;
    public bool ShowUsLocal => MarketCountry.AllowsUsLocalTeleconsult(HomeCountryCode);

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/Virtual/Service?consultationId={ConsultationId}" });

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/Index");

        await LoadHomeAndItemsAsync();
        var sub = await _care.GetActiveAsync(_auth.CurrentUserId.Value);
        HasCare = sub != null;
        CareRemaining = sub != null ? _care.RemainingQuickConsults(sub) : 0;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/Index");

        await LoadHomeAndItemsAsync();
        var sub = _auth.CurrentUserId is int uid ? await _care.GetActiveAsync(uid) : null;
        HasCare = sub != null;
        CareRemaining = sub != null ? _care.RemainingQuickConsults(sub) : 0;

        if (ServiceCode == ServiceCatalogCodes.ChomblyCare)
            return RedirectToPage("/Plans/ChomblyCare", new { consultationId = ConsultationId });

        var item = Items.FirstOrDefault(i => i.Code == ServiceCode);
        if (item is null || !item.IsBookable)
        {
            ErrorMessage = CatalogLocalizer.Loc("Elige un servicio reservable.", "Choose a bookable service.");
            return Page();
        }

        if (item.Code == ServiceCatalogCodes.VetLocal30)
        {
            if (!ShowUsLocal)
                return RedirectToPage("/Vet/International/Matches", new { consultationId = ConsultationId });
            if (!Consultation.HasActiveVcpr)
                return RedirectToPage("/Vet/Virtual/Eligibility", new { consultationId = ConsultationId });
        }

        Consultation.ServiceCatalogCode = item.Code;
        Consultation.UsesCareBenefit = false;
        Consultation.Status = ConsultationStatus.EligibilityVerified;
        await _flow.TouchAsync(Consultation);

        if (item.Code == ServiceCatalogCodes.VetIntl30)
            return RedirectToPage("/Vet/International/Home", new { consultationId = ConsultationId });

        return RedirectToPage("/Vet/Virtual/Providers", new { consultationId = ConsultationId });
    }

    public async Task<IActionResult> OnPostUseCareAsync()
    {
        if (_auth.CurrentUserId is null) return RedirectToPage("/Account/Login");
        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/Index");

        if (!await _care.HasQuickConsultAvailableAsync(_auth.CurrentUserId.Value))
            return RedirectToPage("/Plans/ChomblyCare", new { consultationId = ConsultationId });

        Consultation.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
        Consultation.UsesCareBenefit = true;
        Consultation.Status = ConsultationStatus.EligibilityVerified;
        await _flow.TouchAsync(Consultation);
        return RedirectToPage("/Vet/International/Home", new { consultationId = ConsultationId });
    }

    private async Task LoadHomeAndItemsAsync()
    {
        if (_auth.CurrentUserId is int userId)
        {
            var user = await _db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.CountryCode, u.City, u.Latitude, u.Longitude })
                .FirstOrDefaultAsync();
            HomeCountryCode = MarketCountry.ResolveForUser(user?.CountryCode, user?.City, user?.Latitude, user?.Longitude);
        }

        var all = await _catalog.ListActiveAsync();
        Items = ShowUsLocal
            ? all
            : all.Where(i => i.Code != ServiceCatalogCodes.VetLocal30).ToList();
    }
}
