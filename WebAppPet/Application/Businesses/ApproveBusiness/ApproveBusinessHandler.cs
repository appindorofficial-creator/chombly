using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Businesses.ApproveBusiness;

/// <summary>Publishes the business and tells the owner by notification and email.</summary>
public class ApproveBusinessHandler
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;

    public ApproveBusinessHandler(AppDbContext db, IEmailService email)
    {
        _db = db;
        _email = email;
    }

    /// <returns>The business name, or null when the business does not exist.</returns>
    public async Task<string?> HandleAsync(ApproveBusinessCommand command, CancellationToken ct = default)
    {
        var business = await _db.Groomers
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == command.BusinessId, ct);
        if (business is null)
            return null;

        business.PublishStatus = BusinessPublishStatus.Approved;
        business.IsActive = true;
        business.IsVerified = true;
        _db.Notifications.Add(new AppNotification
        {
            UserId = business.UserId,
            Title = "¡Negocio publicado!",
            Message = $"{business.BusinessName} ya aparece en Chombly.",
            Type = "business"
        });
        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(business.User?.Email))
        {
            await _email.SendAsync(
                business.User.Email,
                "Chombly: ¡tu negocio ya está publicado!",
                $"""
                <p>Hola {business.User.FullName},</p>
                <p><strong>{business.BusinessName}</strong> ya aparece en Chombly y los clientes pueden reservar.</p>
                <p>Entra a tu panel para gestionar agenda y citas.</p>
                <p>— Equipo Chombly</p>
                """,
                ct);
        }

        return business.BusinessName;
    }
}
