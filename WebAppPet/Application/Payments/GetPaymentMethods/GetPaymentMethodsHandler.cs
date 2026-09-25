using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Application.Payments.GetPaymentMethods;

/// <summary>The user's saved cards, default first.</summary>
public class GetPaymentMethodsHandler
{
    private readonly AppDbContext _db;

    public GetPaymentMethodsHandler(AppDbContext db) => _db = db;

    public Task<List<PaymentMethod>> HandleAsync(GetPaymentMethodsQuery query, CancellationToken ct = default) =>
        _db.PaymentMethods
            .Where(p => p.UserId == query.UserId)
            .OrderByDescending(p => p.IsDefault)
            .ToListAsync(ct);
}
