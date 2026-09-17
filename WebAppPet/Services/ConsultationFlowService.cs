using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Services;

public class ConsultationFlowService
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public ConsultationFlowService(AppDbContext db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<Consultation?> GetOwnedAsync(int id, CancellationToken ct = default)
    {
        var uid = _auth.CurrentUserId;
        if (uid is null) return null;
        return await _db.Consultations
            .Include(c => c.Pet)
            .Include(c => c.Provider)
            .FirstOrDefaultAsync(c => c.Id == id && c.ClientId == uid.Value, ct);
    }

    public async Task<Consultation> StartVirtualAsync(CancellationToken ct = default)
    {
        if (_auth.CurrentUserId is not int uid)
            throw new InvalidOperationException("Login required.");

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid, ct);
        var market = BusinessMarketResolver.ResolveUser(user?.City, user?.Latitude, user?.Longitude);
        var resolved = GeoHelper.ResolveUsState(user?.City, user?.Latitude, user?.Longitude);
        string state;
        string? contextCountry;
        if (market == BusinessMarket.Colombia ||
            (market == BusinessMarket.Unknown && string.Equals(resolved, "Other", StringComparison.OrdinalIgnoreCase)))
        {
            state = "Other";
            contextCountry = "CO";
        }
        else
        {
            state = resolved ?? "NC";
            contextCountry = market == BusinessMarket.UnitedStates ||
                             (resolved is not null && !string.Equals(resolved, "Other", StringComparison.OrdinalIgnoreCase))
                ? "US"
                : null;
        }

        var c = new Consultation
        {
            ClientId = uid,
            Modality = VetModality.Virtual,
            Status = ConsultationStatus.Draft,
            PetUsState = state,
            ContextCountry = contextCountry,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Consultations.Add(c);
        await _db.SaveChangesAsync(ct);
        return c;
    }

    public async Task TouchAsync(Consultation c, CancellationToken ct = default)
    {
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
