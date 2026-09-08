using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
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

    [BindProperty]
    public string? ClinicalNotes { get; set; }

    public Consultation? Consultation { get; set; }
    public ServiceCatalogItem? CatalogItem { get; set; }
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/Virtual/Summary/{Id}" });

        Consultation = await _db.Consultations
            .Include(c => c.Pet)
            .Include(c => c.Provider)
            .FirstOrDefaultAsync(c => c.Id == Id && c.ClientId == _auth.CurrentUserId);

        if (Consultation is null) return RedirectToPage("/Vet/Index");

        CatalogItem = await _catalog.GetAsync(Consultation.ServiceCatalogCode ?? "");
        ClinicalNotes = Consultation.ClinicalNotes;
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (_auth.CurrentUserId is null) return RedirectToPage("/Account/Login");

        Consultation = await _db.Consultations
            .Include(c => c.Pet)
            .Include(c => c.Provider)
            .FirstOrDefaultAsync(c => c.Id == Id && c.ClientId == _auth.CurrentUserId);

        if (Consultation is null) return RedirectToPage("/Vet/Index");

        Consultation.ClinicalNotes = ClinicalNotes?.Trim();
        Consultation.UpdatedAt = DateTime.UtcNow;
        if (Consultation.Status == ConsultationStatus.Scheduled)
            Consultation.Status = ConsultationStatus.FollowUpOpen;

        _db.Notifications.Add(new AppNotification
        {
            UserId = _auth.CurrentUserId.Value,
            Title = CatalogLocalizer.Loc("Seguimiento veterinario", "Vet follow-up"),
            Message = CatalogLocalizer.Loc(
                $"Recordatorio guardado para {Consultation.Pet?.Name}.",
                $"Reminder saved for {Consultation.Pet?.Name}."),
            Type = "vet-followup",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        CatalogItem = await _catalog.GetAsync(Consultation.ServiceCatalogCode ?? "");
        Message = CatalogLocalizer.Loc("Guardado en historial.", "Saved to history.");
        return Page();
    }
}
