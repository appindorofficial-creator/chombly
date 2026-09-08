using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;

namespace WebAppPet.Services;

public class VcprService
{
    private readonly AppDbContext _db;

    public VcprService(AppDbContext db) => _db = db;

    public async Task<bool> HasActiveAsync(int petId, string usState, CancellationToken ct = default)
    {
        var state = (usState ?? "NC").Trim().ToUpperInvariant();
        var cutoff = DateTime.UtcNow.AddMonths(-12);
        return await _db.VcprRecords.AsNoTracking().AnyAsync(v =>
            v.PetId == petId &&
            v.IsActive &&
            v.UsState == state &&
            v.ExamDate >= cutoff, ct);
    }
}
