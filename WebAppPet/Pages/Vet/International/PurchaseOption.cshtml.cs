using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.International;

public class PurchaseOptionModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;
    private readonly ServiceCatalogService _catalog;
    private readonly ChomblyCareService _care;
    private readonly AppDbContext _db;

    public PurchaseOptionModel(
        AuthService auth,
        ConsultationFlowService flow,
        ServiceCatalogService catalog,
        ChomblyCareService care,
        AppDbContext db)
    {
        _auth = auth;
        _flow = flow;
        _catalog = catalog;
        _care = care;
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    public Consultation? Consultation { get; set; }
    public GroomerProfile? Provider { get; set; }
    public ServiceCatalogItem? IntlItem { get; set; }
    public ServiceCatalogItem? CareItem { get; set; }
    public bool HasCareBenefit { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/International/PurchaseOption?consultationId={ConsultationId}" });

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation?.ProviderId is null) return RedirectToPage("/Vet/International/Matches", new { consultationId = ConsultationId });

        Provider = await _db.Groomers.AsNoTracking().FirstOrDefaultAsync(g => g.Id == Consultation.ProviderId);
        IntlItem = await _catalog.GetAsync(ServiceCatalogCodes.VetIntl30);
        CareItem = await _catalog.GetAsync(ServiceCatalogCodes.ChomblyCare);
        HasCareBenefit = await _care.HasQuickConsultAvailableAsync(_auth.CurrentUserId.Value);
        return Page();
    }

    public async Task<IActionResult> OnPostPayOnceAsync()
    {
        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/International/Home");
        Consultation.UsesCareBenefit = false;
        Consultation.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
        await _flow.TouchAsync(Consultation);
        return RedirectToPage("/Vet/International/Consent", new { consultationId = ConsultationId, careBenefit = 0 });
    }

    public async Task<IActionResult> OnPostCareAsync()
    {
        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/International/Home");

        if (_auth.CurrentUserId is int uid && await _care.HasQuickConsultAvailableAsync(uid))
        {
            Consultation.UsesCareBenefit = true;
            await _flow.TouchAsync(Consultation);
            return RedirectToPage("/Vet/International/Consent", new { consultationId = ConsultationId, careBenefit = 1 });
        }

        return RedirectToPage("/Plans/ChomblyCare", new { consultationId = ConsultationId });
    }
}
