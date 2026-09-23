using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.Virtual;

public class EligibilityModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;
    private readonly AppDbContext _db;

    public EligibilityModel(AuthService auth, ConsultationFlowService flow, AppDbContext db)
    {
        _auth = auth;
        _flow = flow;
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    public Consultation? Consultation { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/Virtual/Eligibility?consultationId={ConsultationId}" });

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/Index");

        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == _auth.CurrentUserId.Value)
            .Select(u => new { u.CountryCode, u.City, u.Latitude, u.Longitude })
            .FirstOrDefaultAsync();
        var home = MarketCountry.ResolveForUser(user?.CountryCode, user?.City, user?.Latitude, user?.Longitude);

        // VCPR / local US teleconsult is not a Colombia product path — send to guidance.
        if (!MarketCountry.AllowsUsLocalTeleconsult(home))
        {
            Consultation.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
            Consultation.MatchMode = IntlMatchMode.Best;
            Consultation.Status = ConsultationStatus.SafetyScreened;
            await _flow.TouchAsync(Consultation);
            return RedirectToPage("/Vet/International/Matches", new { consultationId = ConsultationId });
        }

        return Page();
    }
}
