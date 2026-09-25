using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;

namespace WebAppPet.Application.Payments.AddPaymentMethod;

/// <summary>
/// Saves a simulated card (brand, last 4, holder, expiry). The first card becomes the default.
/// </summary>
public class AddPaymentMethodHandler
{
    private readonly AppDbContext _db;
    private readonly IStringLocalizer<SharedResource> _L;

    public AddPaymentMethodHandler(AppDbContext db, IStringLocalizer<SharedResource> L)
    {
        _db = db;
        _L = L;
    }

    public async Task<AddPaymentMethodResult> HandleAsync(AddPaymentMethodCommand command, CancellationToken ct = default)
    {
        var card = CardValidator.Validate(command.CardNumber, command.Expiry, command.Cvv, command.HolderName);
        if (!card.Ok)
            return new AddPaymentMethodResult(card.Reason);

        if (command.MakeDefault)
        {
            var current = await _db.PaymentMethods.Where(p => p.UserId == command.UserId && p.IsDefault).ToListAsync(ct);
            foreach (var c in current) c.IsDefault = false;
        }

        _db.PaymentMethods.Add(new PaymentMethod
        {
            UserId = command.UserId,
            Brand = card.Brand == CardValidator.CardBrand.Unknown
                ? _L["Pay_BrandUnknown"].Value
                : card.BrandName,
            Last4 = card.Last4,
            HolderName = card.HolderName,
            ExpMonth = card.ExpMonth,
            ExpYear = card.ExpYear,
            IsDefault = command.MakeDefault || !await _db.PaymentMethods.AnyAsync(p => p.UserId == command.UserId, ct)
        });

        await _db.SaveChangesAsync(ct);
        return new AddPaymentMethodResult(CardValidator.FailReason.None);
    }
}
