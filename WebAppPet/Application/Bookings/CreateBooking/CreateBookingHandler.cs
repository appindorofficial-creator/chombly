using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Bookings.Shared;
using WebAppPet.Application.Promotions.ApplyPromoCode;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Application.Bookings.CreateBooking;

public class CreateBookingHandler
{
    private readonly AppDbContext _db;
    private readonly ApplyPromoCodeHandler _promo;

    public CreateBookingHandler(AppDbContext db, ApplyPromoCodeHandler promo)
    {
        _db = db;
        _promo = promo;
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

        return CreateBookingResult.Created(appt.Id, quote);
    }
}
