using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Businesses.RejectBusiness;

/// <summary>Hides the business and asks the owner, by notification and email, to fix the profile and resubmit.</summary>
public class RejectBusinessHandler
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;

    public RejectBusinessHandler(AppDbContext db, IEmailService email)
    {
        _db = db;
        _email = email;
    }

    /// <returns>The business name, or null when the business does not exist.</returns>
    public async Task<string?> HandleAsync(RejectBusinessCommand command, CancellationToken ct = default)
    {
        var business = await _db.Groomers
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == command.BusinessId, ct);
        if (business is null)
            return null;

        business.PublishStatus = BusinessPublishStatus.Rejected;
        business.IsActive = false;
        business.IsVerified = false;
        _db.Notifications.Add(new AppNotification
        {
            UserId = business.UserId,
            Title = "Solicitud no aprobada",
            Message = "Tu negocio no fue aprobado. Revisa el perfil y vuelve a solicitar desde el panel.",
            Type = "business"
        });
        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(business.User?.Email))
        {
            await _email.SendAsync(
                business.User.Email,
                "Chombly: solicitud de negocio no aprobada",
                $"""
                <p>Hola {business.User.FullName},</p>
                <p>La solicitud de <strong>{business.BusinessName}</strong> no fue aprobada por ahora.</p>
                <p>Revisa tu perfil en el panel y vuelve a enviarla cuando esté completa.</p>
                <p>— Equipo Chombly</p>
                """,
                ct);
        }

        return business.BusinessName;
    }
}
