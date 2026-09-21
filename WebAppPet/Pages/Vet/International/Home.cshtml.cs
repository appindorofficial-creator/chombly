using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.International;

public class HomeModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;
    private readonly VetAuditService _audit;
    private readonly AppDbContext _db;

    public HomeModel(
        AuthService auth,
        ConsultationFlowService flow,
        VetAuditService audit,
        AppDbContext db)
    {
        _auth = auth;
        _flow = flow;
        _audit = audit;
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public int? ConsultationId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? PetId { get; set; }

    public string? PetName { get; set; }
    public string? PetBreed { get; set; }
    public string PreferredLanguage { get; set; } = "es";
    public string LanguageLabel =>
        string.Equals(PreferredLanguage, "en", StringComparison.OrdinalIgnoreCase) ? "English" : "Español";

    public async Task OnGetAsync()
    {
        await LoadContextAsync();
    }

    public async Task<IActionResult> OnPostFindAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = "/Vet/International/Home" });

        var c = ConsultationId is int id
            ? await _flow.GetOwnedAsync(id)
            : null;
        c ??= await _flow.StartVirtualAsync();
        c.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
        c.MatchMode = IntlMatchMode.Best;

        if (PetId is int pid && pid > 0 && c.PetId is null)
            c.PetId = pid;

        // Seed breed from pet so Matches can filter/rank without MatchMode.
        if (string.IsNullOrWhiteSpace(c.PreferredBreed) && c.PetId is int petId)
        {
            var breed = await _db.Pets.AsNoTracking()
                .Where(p => p.Id == petId)
                .Select(p => p.Breed)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(breed))
                c.PreferredBreed = breed.Trim();
        }

        await _flow.TouchAsync(c);
        await _audit.LogAsync("intl_landing_view", _auth.CurrentUserId, "Consultation", c.Id);

        if (c.PetId is null || string.IsNullOrWhiteSpace(c.PetUsState))
            return RedirectToPage("/Vet/Virtual/Pet", new { consultationId = c.Id, next = "intl" });

        // Skip MatchMode — go straight to results (filters live on Matches / later slice).
        return RedirectToPage("/Vet/International/Matches", new { consultationId = c.Id });
    }

    private async Task LoadContextAsync()
    {
        if (_auth.CurrentUserId is int uid)
        {
            PreferredLanguage = await _db.Users.AsNoTracking()
                .Where(u => u.Id == uid)
                .Select(u => u.PreferredLanguage)
                .FirstOrDefaultAsync() ?? "es";
        }

        if (string.IsNullOrWhiteSpace(PreferredLanguage))
            PreferredLanguage = CatalogLocalizer.IsEnglish() ? "en" : "es";

        int? petId = PetId;
        if (petId is null && ConsultationId is int cid)
        {
            var c = await _flow.GetOwnedAsync(cid);
            petId = c?.PetId;
            if (!string.IsNullOrWhiteSpace(c?.PreferredBreed))
                PetBreed = c.PreferredBreed;
        }

        if (petId is int pid)
        {
            var pet = await _db.Pets.AsNoTracking()
                .Where(p => p.Id == pid)
                .Select(p => new { p.Name, p.Breed })
                .FirstOrDefaultAsync();
            if (pet != null)
            {
                PetName = pet.Name;
                if (string.IsNullOrWhiteSpace(PetBreed))
                    PetBreed = pet.Breed;
            }
        }
    }
}
