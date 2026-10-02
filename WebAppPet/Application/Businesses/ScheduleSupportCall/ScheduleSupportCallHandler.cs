using Microsoft.EntityFrameworkCore;
using WebAppPet.Infrastructure.Email;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Localization;

namespace WebAppPet.Application.Businesses.ScheduleSupportCall;

public enum ScheduleSupportCallError
{
    None,
    InvalidDay,
    InvalidSlot,
    NotFound
}

public sealed record ScheduleSupportCallResult(ScheduleSupportCallError Error, DateTime? When = null);

/// <summary>Books the onboarding support call and emails the owner and the Chombly team.</summary>
public class ScheduleSupportCallHandler
{
    public const string TeamEmail = "appindorofficial@gmail.com";

    private readonly AppDbContext _db;
    private readonly IEmailService _email;

    public ScheduleSupportCallHandler(AppDbContext db, IEmailService email)
    {
        _db = db;
        _email = email;
    }

    public async Task<ScheduleSupportCallResult> HandleAsync(ScheduleSupportCallCommand command, CancellationToken ct = default)
    {
        if (!DateTime.TryParse(command.Day, out var day))
            return new(ScheduleSupportCallError.InvalidDay);
        if (!DateTime.TryParse($"{day:yyyy-MM-dd} {command.Slot}", out var when))
            return new(ScheduleSupportCallError.InvalidSlot);

        var business = await _db.Groomers.Include(g => g.User)
            .FirstOrDefaultAsync(g => g.Id == command.BusinessId, ct);
        if (business is null)
            return new(ScheduleSupportCallError.NotFound);

        business.SupportCallAt = when;
        await _db.SaveChangesAsync(ct);

        await _email.SendAsync(
            business.User?.Email ?? "",
            CatalogLocalizer.Loc("Chombly: llamada agendada", "Chombly: call scheduled"),
            CatalogLocalizer.Loc(
                $"<p>Hola {business.User?.FullName},</p><p>Tu llamada de soporte quedó para <strong>{when:ddd d MMM · h:mm tt}</strong>.</p><p>— Equipo Chombly</p>",
                $"<p>Hi {business.User?.FullName},</p><p>Your support call is set for <strong>{when:ddd d MMM · h:mm tt}</strong>.</p><p>— Chombly team</p>"),
            ct);

        await _email.SendAsync(
            TeamEmail,
            $"Llamada soporte — {business.BusinessName}",
            $"<p>{business.BusinessName} agendó llamada: {when:g}</p><p>{business.User?.Email} · {business.Phone}</p>",
            ct);

        return new(ScheduleSupportCallError.None, when);
    }
}
