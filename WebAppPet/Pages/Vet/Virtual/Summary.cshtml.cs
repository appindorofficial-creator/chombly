using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.Virtual;

public class SummaryModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly ServiceCatalogService _catalog;

    public SummaryModel(AppDbContext db, AuthService auth, ServiceCatalogService catalog)
    {
        _db = db;
        _auth = auth;
        _catalog = catalog;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public Consultation? Consultation { get; set; }
    public ServiceCatalogItem? CatalogItem { get; set; }
    public string BackHref { get; private set; } = "/Appointments";
    public string? PetHistoryHref { get; private set; }
    public bool UsingCare { get; private set; }
    public bool IsIntl { get; private set; }
    public bool HasProviderNotes { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/Virtual/Summary/{Id}" });

        if (!await LoadAsync()) return RedirectToPage("/Vet/Index");
        return Page();
    }

    private async Task<bool> LoadAsync()
    {
        Consultation = await _db.Consultations
            .AsNoTracking()
            .Include(c => c.Pet)
            .Include(c => c.Provider)
            .FirstOrDefaultAsync(c => c.Id == Id && c.ClientId == _auth.CurrentUserId);

        if (Consultation is null) return false;

        BackHref = !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
            ? ReturnUrl!
            : Url.Page("/Appointments/Index") ?? "/Appointments";

        if (Consultation.PetId is int petId)
            PetHistoryHref = Url.Page("/Pets/History", new { id = petId, returnUrl = Url.Page("/Vet/Virtual/Summary", new { id = Id }) });

        CatalogItem = await _catalog.GetAsync(Consultation.ServiceCatalogCode ?? "");
        UsingCare = Consultation.UsesCareBenefit || Consultation.PriceCharged <= 0m;
        IsIntl = string.Equals(Consultation.ServiceCatalogCode, ServiceCatalogCodes.VetIntl30, StringComparison.OrdinalIgnoreCase);
        HasProviderNotes = !string.IsNullOrWhiteSpace(Consultation.ClinicalNotes);
        return true;
    }
}
