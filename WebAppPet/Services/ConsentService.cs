using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Services;

public class ConsentService
{
    public const string DocTerms = "terms_of_service";
    public const string DocPrivacy = "privacy_policy";
    public const string DocIntlOrientation = "intl_orientation_consent";
    public const string DocMedia = "media_use_consent";
    public const string Version = "1.0";

    private readonly AppDbContext _db;

    public ConsentService(AppDbContext db) => _db = db;

    public async Task SaveAsync(
        int userId,
        int? consultationId,
        IEnumerable<(string Key, bool Accepted)> boxes,
        string? ip,
        string? userAgent,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        foreach (var (key, accepted) in boxes)
        {
            if (!accepted) continue;
            _db.ConsentRecords.Add(new ConsentRecord
            {
                UserId = userId,
                ConsultationId = consultationId is > 0 ? consultationId : null,
                DocumentKey = key,
                DocumentVersion = Version,
                Accepted = true,
                AcceptedAt = now,
                IpAddress = ip,
                UserAgent = Truncate(userAgent, 260)
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max]);
}
