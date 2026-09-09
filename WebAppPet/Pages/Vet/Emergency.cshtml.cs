using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet;

public class EmergencyModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly VetAuditService _audit;

    public EmergencyModel(AppDbContext db, AuthService auth, VetAuditService audit)
    {
        _db = db;
        _auth = auth;
        _audit = audit;
    }

    [BindProperty(SupportsGet = true)]
    public int? ConsultationId { get; set; }

    public List<GroomerProfile> Clinics { get; set; } = new();

    public async Task OnGetAsync()
    {
        Clinics = await _db.Groomers.AsNoTracking()
            .Include(g => g.Category)
            .Where(g => g.IsActive &&
                        g.PublishStatus == BusinessPublishStatus.Approved &&
                        g.OffersEmergency24x7 &&
                        (g.Category == null || g.Category.Slug == "vet"))
            .OrderByDescending(g => g.Rating)
            .Take(20)
            .ToListAsync();

        if (Clinics.Count == 0)
        {
            Clinics = await _db.Groomers.AsNoTracking()
                .Include(g => g.Category)
                .Where(g => g.IsActive &&
                            g.PublishStatus == BusinessPublishStatus.Approved &&
                            g.Category != null &&
                            g.Category.Slug == "vet")
                .OrderByDescending(g => g.Rating)
                .Take(10)
                .ToListAsync();
        }

        if (ConsultationId is int cid)
        {
            var c = await _db.Consultations.FirstOrDefaultAsync(x => x.Id == cid);
            if (c != null && (_auth.CurrentUserId == null || c.ClientId == _auth.CurrentUserId))
            {
                c.Status = ConsultationStatus.EscalatedToEmergency;
                c.Modality = VetModality.Emergency;
                c.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }

        await _audit.LogAsync("escalated_emergency", _auth.CurrentUserId, "Consultation", ConsultationId,
            new { clinics = Clinics.Count });
    }
}
