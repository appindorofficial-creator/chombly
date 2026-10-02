using WebAppPet.Application.Common;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Businesses.EnsureHotelCoreExtras;

/// <summary>Adds the bath and medication extras a hotel is missing, at the market default price.</summary>
public class EnsureHotelCoreExtrasHandler
{
    private readonly AppDbContext _db;

    public EnsureHotelCoreExtrasHandler(AppDbContext db) => _db = db;

    public Task HandleAsync(EnsureHotelCoreExtrasCommand command, CancellationToken ct = default) =>
        HotelCoreExtras.EnsureMissingAsync(_db, command.HotelIds);
}
