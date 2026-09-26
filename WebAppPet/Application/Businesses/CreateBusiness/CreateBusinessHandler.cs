using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebAppPet.Application.Businesses.Shared;
using WebAppPet.Data;
using WebAppPet.Infrastructure.Security;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Businesses.CreateBusiness;

/// <summary>
/// Last step of the registration wizard. Creates the account (or converts the signed-in client),
/// then the business pending review with its services, schedule and hotel extras. Notifies the owner
/// and the admins. The page signs the user in again so the new role takes effect.
/// </summary>
public class CreateBusinessHandler
{
    public const int AgendaDays = 60;
    private const decimal FallbackStartingPrice = 35;
    private const double KmPerMile = 1.609344;

    private readonly AppDbContext _db;
    private readonly AvailabilityService _availability;
    private readonly IEmailService _email;
    private readonly SmtpOptions _smtp;

    public CreateBusinessHandler(
        AppDbContext db, AvailabilityService availability, IEmailService email, IOptions<SmtpOptions> smtp)
    {
        _db = db;
        _availability = availability;
        _email = email;
        _smtp = smtp.Value;
    }

    public async Task<CreateBusinessResult> HandleAsync(CreateBusinessCommand command, CancellationToken ct = default)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == command.PrimaryCategoryId, ct);
        if (category is null)
            return CreateBusinessResult.Fail(RegistrationError.InvalidCategory);

        var (userError, user) = await PrepareUserAsync(command, ct);
        if (user is null)
            return CreateBusinessResult.Fail(userError);
        await _db.SaveChangesAsync(ct);

        var (names, prices) = BusinessServiceDefaults.Normalize(command.ServiceNames, command.ServicePrices);
        var work = command.WorkModeKey switch
        {
            "domicilio" => WorkMode.Mobile,
            "ambas" => WorkMode.Both,
            _ => WorkMode.Local
        };
        var kind = command.ProviderKindKey == "independent" ? ProviderKind.Independent : ProviderKind.Business;
        var hasCoords = !(command.Latitude == 0 && command.Longitude == 0);
        var market = BusinessMarketResolver.ResolveUser(
            command.City, hasCoords ? command.Latitude : null, hasCoords ? command.Longitude : null);

        var firstPrice = prices.FirstOrDefault(p => p > 0);
        if (firstPrice <= 0) firstPrice = FallbackStartingPrice;
        var city = command.City.Trim();
        var coverUrl = string.IsNullOrWhiteSpace(command.CoverUrl) ? null : command.CoverUrl.Trim();

        var business = new GroomerProfile
        {
            UserId = user.Id,
            CategoryId = category.Id,
            ExtraCategoryIds = GroomerProfile.JoinExtraCategoryIds(command.CategoryIds, category.Id),
            BusinessName = command.BusinessName.Trim(),
            ProviderKind = kind,
            WorkMode = work,
            Type = work switch
            {
                WorkMode.Mobile => GroomerType.Mobile,
                WorkMode.Both => GroomerType.InHome,
                _ => GroomerType.Salon
            },
            ServiceAreaMiles = ToStoredMiles(command.ServiceArea, usesKm: market == BusinessMarket.Colombia),
            LicenseCountry = MarketCountry.Normalize(
                market == BusinessMarket.Unknown ? user.CountryCode : MarketCountry.FromMarket(market)),
            Address = string.IsNullOrWhiteSpace(command.Address) ? city : command.Address.Trim(),
            City = city,
            Latitude = command.Latitude,
            Longitude = command.Longitude,
            Phone = command.Phone,
            About = command.About.Trim(),
            LogoUrl = string.IsNullOrWhiteSpace(command.LogoUrl) ? null : command.LogoUrl.Trim(),
            CoverUrl = coverUrl,
            ImageUrl = coverUrl ?? $"/images/categories/cat-{category.Slug}-v2.png",
            StartingPrice = firstPrice,
            PriceUnit = category.IsOvernight
                ? CatalogLocalizer.Loc("/ noche", "/ night")
                : CatalogLocalizer.Loc("/ sesión", "/ session"),
            AcceptedSpecies = PetSpecies.DefaultAcceptedList,
            AcceptsSeniorPets = true,
            AcceptsAnxiousPets = true,
            IsActive = false,
            IsVerified = false,
            VerifiedIdentity = true,
            PublishStatus = BusinessPublishStatus.PendingReview,
            Rating = 0,
            ReviewCount = 0
        };
        _db.Groomers.Add(business);
        await _db.SaveChangesAsync(ct);

        AddServices(business.Id, category, names, prices, firstPrice, user.CountryCode);
        await _db.SaveChangesAsync(ct);
        await _availability.SaveWeeklyAndGenerateAsync(business.Id, command.Week, days: AgendaDays);
        await AddHotelExtrasAsync(business.Id, command, user.CountryCode, ct);
        await NotifyAsync(user, business, category, kind, command, ct);

        return new CreateBusinessResult(RegistrationError.None, user, business.Id);
    }

    private async Task<(RegistrationError Error, AppUser? User)> PrepareUserAsync(CreateBusinessCommand command, CancellationToken ct)
    {
        var email = command.Email.Trim().ToLowerInvariant();
        var lat = command.Latitude;
        var lng = command.Longitude;

        if (command.CurrentUserId is int currentUserId)
        {
            var existing = await _db.Users
                .Include(u => u.GroomerProfile)
                .FirstOrDefaultAsync(u => u.Id == currentUserId, ct);
            if (existing is null)
                return (RegistrationError.InvalidSession, null);
            if (existing.GroomerProfile is not null)
                return (RegistrationError.AlreadyBusiness, null);
            if (await _db.Users.AnyAsync(u => u.Email == email && u.Id != existing.Id, ct))
                return (RegistrationError.EmailTaken, null);

            existing.FullName = command.FullName.Trim();
            existing.Email = email;
            existing.Phone = command.Phone;
            existing.City = command.City.Trim();
            existing.Latitude = lat == 0 ? existing.Latitude : lat;
            existing.Longitude = lng == 0 ? existing.Longitude : lng;
            if (lat != 0 && lng != 0)
                existing.LocationUpdatedAt = DateTime.UtcNow;
            MarketCountry.ApplyFromLocation(existing, command.City, lat == 0 ? null : lat, lng == 0 ? null : lng);
            existing.Role = UserRole.Groomer;
            return (RegistrationError.None, existing);
        }

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            return (RegistrationError.EmailTakenLogin, null);

        var user = new AppUser
        {
            FullName = command.FullName.Trim(),
            Email = email,
            PasswordHash = PasswordHasher.Hash(command.Password ?? ""),
            Phone = command.Phone,
            City = command.City.Trim(),
            Latitude = lat == 0 ? null : lat,
            Longitude = lng == 0 ? null : lng,
            LocationUpdatedAt = lat == 0 && lng == 0 ? null : DateTime.UtcNow,
            Role = UserRole.Groomer
        };
        MarketCountry.ApplyFromLocation(user);
        _db.Users.Add(user);
        return (RegistrationError.None, user);
    }

    private void AddServices(
        int businessId, ServiceCategory category, List<string> names, List<decimal> prices, decimal firstPrice, string? countryCode)
    {
        var step = HotelCoreExtras.SizeStepFor(countryCode);
        var description = $"{CatalogLocalizer.Loc("Servicio de", "Service:")} {category.DisplayName()}";
        for (var i = 0; i < names.Count; i++)
        {
            var price = i < prices.Count ? prices[i] : firstPrice;
            if (price <= 0) continue;
            _db.Services.Add(new GroomerService
            {
                GroomerId = businessId,
                Name = names[i],
                Description = description,
                BillingUnit = category.IsOvernight ? "noche" : "sesion",
                PriceSmall = price,
                PriceMedium = price + step,
                PriceLarge = price + step * 2,
                PriceGiant = price + step * 3,
                DurationMinutes = category.IsOvernight ? ServiceDurations.NightMinutes : ServiceDurations.FromName(names[i])
            });
        }
    }

    private async Task AddHotelExtrasAsync(int businessId, CreateBusinessCommand command, string? countryCode, CancellationToken ct)
    {
        var isHotel = await _db.Categories.AnyAsync(c => c.IsActive
            && command.CategoryIds.Contains(c.Id)
            && c.Slug.ToLower() == "hotel", ct);
        if (!isHotel)
            return;

        var extras = command.HotelExtras;
        await HotelCoreExtras.SyncAsync(_db, businessId, extras.BathPrice, extras.MedsPrice, countryCode);
        if (extras.OffersPrivateCamera)
            await HotelPrivateCameraExtra.SyncAsync(_db, businessId, true, extras.PrivateCameraPrice, countryCode);
    }

    private async Task NotifyAsync(
        AppUser user, GroomerProfile business, ServiceCategory category, ProviderKind kind,
        CreateBusinessCommand command, CancellationToken ct)
    {
        _db.Notifications.Add(new AppNotification
        {
            UserId = user.Id,
            // Stored in Spanish; NotificationLocalizer translates on read.
            Title = "¡Bienvenido a Chombly!",
            Message = "Tu perfil fue creado. Completa la verificación para publicar más rápido.",
            Type = "business"
        });

        var adminIds = await _db.Users.Where(u => u.Role == UserRole.Admin).Select(u => u.Id).ToListAsync(ct);
        foreach (var adminId in adminIds)
        {
            _db.Notifications.Add(new AppNotification
            {
                UserId = adminId,
                Title = "Nuevo negocio pendiente",
                Message = $"{business.BusinessName} ({category.Name}) · {kind}",
                Type = "business"
            });
        }
        await _db.SaveChangesAsync(ct);

        await _email.SendAsync(
            user.Email,
            CatalogLocalizer.Loc("Chombly: perfil de negocio creado", "Chombly: business profile created"),
            CatalogLocalizer.Loc(
                $"<p>Hola {user.FullName},</p><p>¡Bienvenido! Creamos el perfil de <strong>{business.BusinessName}</strong>.</p><p>— Equipo Chombly</p>",
                $"<p>Hi {user.FullName},</p><p>Welcome! We created the profile for <strong>{business.BusinessName}</strong>.</p><p>— Chombly team</p>"),
            ct);

        var adminTo = string.IsNullOrWhiteSpace(_smtp.AdminNotifyEmail) ? _smtp.From : _smtp.AdminNotifyEmail;
        await _email.SendAsync(
            adminTo,
            CatalogLocalizer.Loc(
                $"Chombly: nuevo negocio — {business.BusinessName}",
                $"Chombly: new business — {business.BusinessName}"),
            $"<p><strong>{business.BusinessName}</strong> ({category.DisplayName()}) · {user.Email} · {command.Phone}</p><p>{command.City}</p>",
            ct);
    }

    /// <summary>The wizard offers round local units; storage is always miles.</summary>
    public static int ToStoredMiles(int selected, bool usesKm)
    {
        if (selected <= 0) return 0;
        if (!usesKm) return selected;
        return Math.Max(1, (int)Math.Round(selected / KmPerMile));
    }
}
