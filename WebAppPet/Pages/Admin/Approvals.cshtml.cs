using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Admin;

public class ApprovalsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly IEmailService _email;

    public ApprovalsModel(AppDbContext db, AuthService auth, IEmailService email)
    {
        _db = db;
        _auth = auth;
        _email = email;
    }

    public List<GroomerProfile> Pending { get; set; } = new();
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!_auth.IsAdmin) return RedirectToPage("/Account/Login");
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(int id)
    {
        if (!_auth.IsAdmin) return RedirectToPage("/Account/Login");
        var g = await _db.Groomers
            .Include(x => x.Category)
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (g != null)
        {
            g.PublishStatus = BusinessPublishStatus.Approved;
            g.IsActive = true;
            g.IsVerified = true;
            _db.Notifications.Add(new AppNotification
            {
                UserId = g.UserId,
                Title = "¡Negocio publicado!",
                Message = $"{g.BusinessName} ya aparece en Chombly.",
                Type = "business"
            });
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(g.User?.Email))
            {
                await _email.SendAsync(
                    g.User.Email,
                    "Chombly: ¡tu negocio ya está publicado!",
                    $"""
                    <p>Hola {g.User.FullName},</p>
                    <p><strong>{g.BusinessName}</strong> ya aparece en Chombly y los clientes pueden reservar.</p>
                    <p>Entra a tu panel para gestionar agenda y citas.</p>
                    <p>— Equipo Chombly</p>
                    """);
            }

            Message = $"{g.BusinessName} aprobado y publicado.";
        }
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        if (!_auth.IsAdmin) return RedirectToPage("/Account/Login");
        var g = await _db.Groomers
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (g != null)
        {
            g.PublishStatus = BusinessPublishStatus.Rejected;
            g.IsActive = false;
            g.IsVerified = false;
            _db.Notifications.Add(new AppNotification
            {
                UserId = g.UserId,
                Title = "Solicitud no aprobada",
                Message = "Tu negocio no fue aprobado. Revisa el perfil y vuelve a solicitar desde el panel.",
                Type = "business"
            });
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(g.User?.Email))
            {
                await _email.SendAsync(
                    g.User.Email,
                    "Chombly: solicitud de negocio no aprobada",
                    $"""
                    <p>Hola {g.User.FullName},</p>
                    <p>La solicitud de <strong>{g.BusinessName}</strong> no fue aprobada por ahora.</p>
                    <p>Revisa tu perfil en el panel y vuelve a enviarla cuando esté completa.</p>
                    <p>— Equipo Chombly</p>
                    """);
            }

            Message = $"{g.BusinessName} rechazado.";
        }
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        Pending = await _db.Groomers
            .Include(g => g.Category)
            .Include(g => g.User)
            .Include(g => g.WeeklyHours)
            .Include(g => g.Services)
            .Where(g => g.PublishStatus == BusinessPublishStatus.PendingReview)
            .OrderByDescending(g => g.Id)
            .ToListAsync();
    }
}
