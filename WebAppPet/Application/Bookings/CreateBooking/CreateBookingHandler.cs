using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Bookings.Shared;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Application.Promotions.ApplyPromoCode;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;

namespace WebAppPet.Application.Bookings.CreateBooking;

/// <summary>
/// Creates a pending booking after charging its deposit to the client's card;
/// the rest of the price is paid directly at the business.
/// </summary>
public class CreateBookingHandler
{
    private readonly AppDbContext _db;
    private readonly ApplyPromoCodeHandler _promo;
    private readonly PaymentService _payments;

    public CreateBookingHandler(AppDbContext db, ApplyPromoCodeHandler promo, PaymentService payments)
    {
        _db = db;
        _promo = promo;
        _payments = payments;
    }

    public async Task<CreateBookingResult> HandleAsync(CreateBookingCommand command, CancellationToken ct = default)
    {
        var business = await _db.Groomers.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == command.BusinessId && g.IsActive, ct);
        var serviceOffered = await _db.Services.AsNoTracking()
            .AnyAsync(s => s.Id == command.ServiceId && s.GroomerId == command.BusinessId, ct);
        var petIds = command.PetIds.Distinct().ToList();
        if (business == null || !serviceOffered || petIds.Count == 0)
            return CreateBookingResult.Fail(CreateBookingError.MissingData);

        var pets = await _db.Pets.AsNoTracking()
            .Where(p => petIds.Contains(p.Id) && p.OwnerId == command.ClientId)
            .ToListAsync(ct);
        if (pets.Count != petIds.Count)
            return CreateBookingResult.Fail(CreateBookingError.MissingData);

        var rejected = petIds
            .Select(id => pets.First(p => p.Id == id))
            .FirstOrDefault(p => !business.AcceptsSpecies(p.Species)
                                 || (command.AllowedSpecies != null
                                     && !command.AllowedSpecies.Contains(p.Species, StringComparer.OrdinalIgnoreCase)));
        if (rejected != null)
            return CreateBookingResult.Fail(CreateBookingError.SpeciesNotAccepted) with { RejectedSpecies = rejected.Species };

        var promo = await _promo.HandleAsync(
            new ApplyPromoCodeCommand(command.ClientId, command.PromoCode, command.Subtotal), ct);
        if (!string.IsNullOrWhiteSpace(command.PromoCode) && !promo.IsValid)
            return CreateBookingResult.Fail(CreateBookingError.InvalidPromo) with { PromoError = promo.ErrorMessage };

        var quote = BookingPricing.Quote(command.Subtotal, promo, command.MinimumDeposit);

        PaymentTransaction? deposit = null;
        if (quote.Deposit > 0)
        {
            var card = await _payments.FindCardAsync(command.ClientId, command.PaymentMethodId, ct);
            if (card is null)
                return CreateBookingResult.Fail(CreateBookingError.NoPaymentMethod) with
                {
                    PaymentError = CatalogLocalizer.Loc(
                        "Agrega un método de pago para pagar el anticipo.",
                        "Add a payment method to pay the deposit.")
                };

            deposit = await _payments.ChargeAsync(new ChargeRequest
            {
                UserId = command.ClientId,
                Card = card,
                Amount = quote.Deposit,
                ServiceTotal = quote.Total,
                Purpose = PaymentPurpose.BookingDeposit,
                Description = $"Deposit · {business.BusinessName}",
                ProviderId = business.Id
            }, ct);
            if (deposit.Status != PaymentTransactionStatus.Succeeded)
                return CreateBookingResult.Fail(CreateBookingError.PaymentDeclined) with
                {
                    PaymentError = PaymentFailureCodes.Message(deposit.FailureCode)
                };
        }

        var appt = new Appointment
        {
            ClientId = command.ClientId,
            PetId = petIds[0],
            GroomerId = business.Id,
            ServiceId = command.ServiceId,
            ScheduledAt = command.StartUtc,
            EndAt = command.EndUtc,
            Nights = command.Nights,
            Status = AppointmentStatus.Pending,
            TotalPrice = quote.Total,
            DepositPaid = quote.Deposit,
            PromoCode = quote.Discount > 0 ? promo.NormalizedCode : null,
            DiscountAmount = quote.Discount,
            Notes = command.NoteParts.Count > 0 ? string.Join(" · ", command.NoteParts) : null
        };

        foreach (var extra in command.Extras)
        {
            appt.Extras.Add(new AppointmentExtra
            {
                ServiceExtraId = extra.ServiceExtraId,
                Name = extra.Name,
                Price = extra.Price
            });
        }

        _db.Appointments.Add(appt);
        _db.Notifications.Add(new AppNotification
        {
            UserId = command.ClientId,
            Title = command.ClientNotice.Title,
            Message = command.ClientNotice.Message,
            Type = "appointment"
        });
        _db.Notifications.Add(new AppNotification
        {
            UserId = business.UserId,
            Title = command.BusinessNotice.Title,
            Message = command.BusinessNotice.Message,
            Type = "appointment"
        });
        await _db.SaveChangesAsync(ct);
        if (deposit is not null)
            await _payments.AttachAppointmentAsync(deposit, appt.Id, ct);

        return CreateBookingResult.Created(appt.Id, quote);
    }
}
