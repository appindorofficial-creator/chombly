using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;
    private readonly VetAuditService _audit;

    public IndexModel(AppDbContext db, AuthService auth, ConsultationFlowService flow, VetAuditService audit)
    {
        _db = db;
        _auth = auth;
        _flow = flow;
        _audit = audit;
    }

    [BindProperty(SupportsGet = true)]
    public int? PetId { get; set; }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostStartVirtualAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = "/Vet" });

        var c = await _flow.StartVirtualAsync();
        if (PetId is int pid && pid > 0)
        {
            var owns = await _db.Pets.AsNoTracking()
                .AnyAsync(p => p.Id == pid && p.OwnerId == _auth.CurrentUserId);
            if (owns)
            {
                c.PetId = pid;
                await _flow.TouchAsync(c);
            }
        }

        await _audit.LogAsync("modality_selected", _auth.CurrentUserId, "Consultation", c.Id,
            new { modality = nameof(VetModality.Virtual), petId = c.PetId });
        return RedirectToPage("/Vet/Virtual/Pet", new { consultationId = c.Id });
    }
}
