using System.Text.Json;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Services;

public class VetAuditService
{
    private readonly AppDbContext _db;

    public VetAuditService(AppDbContext db) => _db = db;

    public async Task LogAsync(
        string action,
        int? actorUserId,
        string? entityType = null,
        int? entityId = null,
        object? payload = null,
        CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLogEntry
        {
            Action = action,
            ActorUserId = actorUserId,
            EntityType = entityType,
            EntityId = entityId,
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }
}
