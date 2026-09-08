using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.International;

public class MatchModeModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;
    private readonly AppDbContext _db;
    private readonly VetAuditService _audit;

    public MatchModeModel(AuthService auth, ConsultationFlowService flow, AppDbContext db, VetAuditService audit)
    {
        _auth = auth;
        _flow = flow;
        _db = db;
        _audit = audit;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    [BindProperty]
    public IntlMatchMode Mode { get; set; } = IntlMatchMode.Best;

    public Consultation? Consultation { get; set; }
    public string? PetBreed { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/International/MatchMode?consultationId={ConsultationId}" });

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/International/Home");

        if (Consultation.PetId is int pid)
            PetBreed = await _db.Pets.Where(p => p.Id == pid).Select(p => p.Breed).FirstOrDefaultAsync();

        Mode = Consultation.MatchMode;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/International/Home");

        Consultation.MatchMode = Mode;
        await _flow.TouchAsync(Consultation);
        await _audit.LogAsync("mode_selected", _auth.CurrentUserId, "Consultation", ConsultationId, new { Mode });

        return Mode switch
        {
            IntlMatchMode.Country => RedirectToPage("/Vet/International/Countries", new { consultationId = ConsultationId }),
            IntlMatchMode.Breed => RedirectToPage("/Vet/International/BreedMatch", new { consultationId = ConsultationId }),
            _ => RedirectToPage("/Vet/International/Matches", new { consultationId = ConsultationId })
        };
    }
}
