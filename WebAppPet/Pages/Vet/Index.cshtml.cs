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

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string BackHref { get; private set; } = "/";
    public string HomeCountryCode { get; private set; } = MarketCountry.DefaultIso;
    public bool ShowUsLocalConsult => MarketCountry.IsUnitedStates(HomeCountryCode);

    public async Task<IActionResult> OnGetAsync()
    {
        BackHref = !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
            ? ReturnUrl!
            : Url.Page("/Index") ?? "/";

        if (_auth.CurrentUserId is int userId)
        {
            var user = await _db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.CountryCode, u.City, u.Latitude, u.Longitude })
                .FirstOrDefaultAsync();
            HomeCountryCode = MarketCountry.ResolveForUser(user?.CountryCode, user?.City, user?.Latitude, user?.Longitude);
        }

        return Page();
    }

    /// <summary>Legacy entry — treat as guidance (shortest virtual path).</summary>
    public Task<IActionResult> OnPostStartVirtualAsync() => StartPathAsync("intl", ServiceCatalogCodes.VetIntl30);

    public Task<IActionResult> OnPostStartGuidanceAsync() => StartPathAsync("intl", ServiceCatalogCodes.VetIntl30);

    public async Task<IActionResult> OnPostStartLocalAsync()
    {
        await EnsureHomeCountryAsync();
        // US-state VCPR teleconsult — not offered for Colombia home market.
        if (!ShowUsLocalConsult)
            return await StartPathAsync("intl", ServiceCatalogCodes.VetIntl30);
        return await StartPathAsync("local", ServiceCatalogCodes.VetLocal30);
    }

    private async Task EnsureHomeCountryAsync()
    {
        if (_auth.CurrentUserId is not int userId) return;
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.CountryCode, u.City, u.Latitude, u.Longitude })
            .FirstOrDefaultAsync();
        HomeCountryCode = MarketCountry.ResolveForUser(user?.CountryCode, user?.City, user?.Latitude, user?.Longitude);
    }

    private async Task<IActionResult> StartPathAsync(string next, string catalogCode)
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = "/Vet" });

        var c = await _flow.StartVirtualAsync();
        c.ServiceCatalogCode = catalogCode;
        if (PetId is int pid && pid > 0)
        {
            var owns = await _db.Pets.AsNoTracking()
                .AnyAsync(p => p.Id == pid && p.OwnerId == _auth.CurrentUserId);
            if (owns)
                c.PetId = pid;
        }

        await _flow.TouchAsync(c);
        await _audit.LogAsync("modality_selected", _auth.CurrentUserId, "Consultation", c.Id,
            new { next, catalogCode, petId = c.PetId });

        return RedirectToPage("/Vet/Virtual/Pet", new { consultationId = c.Id, next });
    }
}
