using Microsoft.EntityFrameworkCore;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Businesses.MarkVerificationItem;

/// <summary>
/// The business ticks a verification step as done so the Chombly team can review it.
/// False when the business does not exist or the step is unknown.
/// </summary>
public class MarkVerificationItemHandler
{
    private readonly AppDbContext _db;

    public MarkVerificationItemHandler(AppDbContext db) => _db = db;

    public async Task<bool> HandleAsync(MarkVerificationItemCommand command, CancellationToken ct = default)
    {
        var business = await _db.Groomers.FirstOrDefaultAsync(g => g.Id == command.BusinessId, ct);
        if (business is null)
            return false;

        switch (command.Item)
        {
            case "identity": business.VerifiedIdentity = true; break;
            case "license": business.VerifiedLicense = true; break;
            case "insurance": business.VerifiedInsurance = true; break;
            case "bank": business.VerifiedBank = true; break;
            default: return false;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }
}
