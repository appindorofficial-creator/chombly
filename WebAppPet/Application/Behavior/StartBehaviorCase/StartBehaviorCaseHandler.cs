using Microsoft.EntityFrameworkCore;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Behavior.StartBehaviorCase;

/// <summary>Opens a draft behavior case for the client and returns its id.</summary>
public class StartBehaviorCaseHandler
{
    private readonly AppDbContext _db;

    public StartBehaviorCaseHandler(AppDbContext db) => _db = db;

    public async Task<int> HandleAsync(StartBehaviorCaseCommand command, CancellationToken ct = default)
    {
        int? petId = null;
        if (command.PetId is int requested && requested > 0
            && await _db.Pets.AsNoTracking().AnyAsync(p => p.Id == requested && p.OwnerId == command.ClientId, ct))
            petId = requested;

        var behaviorCase = new BehaviorCase
        {
            ClientId = command.ClientId,
            PetId = petId,
            Status = BehaviorCaseStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.BehaviorCases.Add(behaviorCase);
        await _db.SaveChangesAsync(ct);
        return behaviorCase.Id;
    }
}
