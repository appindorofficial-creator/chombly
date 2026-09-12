using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Account;

public class RegisterBusinessModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly AvailabilityService _availability;
    private readonly IEmailService _email;
    private readonly SmtpOptions _smtp;
    private readonly GoogleMapsOptions _maps;
    private readonly IStringLocalizer<SharedResource> _L;

    public RegisterBusinessModel(
        AppDbContext db,
        AuthService auth,
        AvailabilityService availability,
        IEmailService email,
        IOptions<SmtpOptions> smtp,
        IOptions<GoogleMapsOptions> maps,
        IStringLocalizer<SharedResource> L)
    {
        _db = db;
        _auth = auth;
        _availability = availability;
        _email = email;
        _smtp = smtp.Value;
        _maps = maps.Value;
        _L = L;
    }

    public List<ServiceCategory> Categories { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public bool IsLoggedIn => _auth.IsAuthenticated;

    /// <summary>0 tipo · 1 básico · 2 servicio · 3 agenda · 4 fotos · 5 precios · 6 cuenta · 7 éxito</summary>
    [BindProperty(SupportsGet = true)]
    public int Step { get; set; }

    [BindProperty] public string ProviderKindKey { get; set; } = "business";
    [BindProperty, MaxLength(100)] public string FullName { get; set; } = string.Empty;
    [BindProperty, MaxLength(120)] public string BusinessName { get; set; } = string.Empty;
    [BindProperty, Required, MaxLength(10)] public string Phone { get; set; } = string.Empty;
    [BindProperty] public string Email { get; set; } = string.Empty;
    [BindProperty] public string City { get; set; } = string.Empty;
    [BindProperty] public string Address { get; set; } = string.Empty;
    [BindProperty] public double Latitude { get; set; }
    [BindProperty] public double Longitude { get; set; }
    [BindProperty] public int CategoryId { get; set; }
    [BindProperty] public string WorkModeKey { get; set; } = "local";
    [BindProperty] public List<WeekDayInput> Week { get; set; } = WeekDayInput.DefaultWeek();
    [BindProperty] public int ServiceAreaMiles { get; set; } = 10;
    [BindProperty, MaxLength(800)] public string About { get; set; } = string.Empty;
    [BindProperty] public string? LogoUrl { get; set; }
    [BindProperty] public string? CoverUrl { get; set; }
    [BindProperty] public List<string> ServiceNames { get; set; } = new();
    [BindProperty] public List<decimal> ServicePrices { get; set; } = new();
    [BindProperty] public string Password { get; set; } = string.Empty;
    [BindProperty] public string ConfirmPassword { get; set; } = string.Empty;
    [BindProperty] public bool AcceptTerms { get; set; }

    public int WizardProgress => Step switch
    {
        1 => 25,
        2 => 50,
        3 => 75,
        4 => 100,
        _ => 0
    };

    public async Task OnGetAsync()
    {
        if (Step is < 0 or > 7) Step = 0;
        await PrepareAsync();
        await PrefillFromCurrentUserAsync();
    }

    public Task<IActionResult> OnPostBackAsync() => GoBackAsync();

    public Task<IActionResult> OnPostNextAsync() => GoNextAsync();

    public async Task<IActionResult> OnPostAsync(string? nav)
    {
        if (string.Equals(nav, "back", StringComparison.OrdinalIgnoreCase))
            return await GoBackAsync();
        return await GoNextAsync();
    }

    private async Task<IActionResult> GoBackAsync()
    {
        await PrepareAsync();
        Step = Math.Max(0, Step - 1);
        EnsureDefaultServices();
        if (Step == 1)
            await PrefillFromCurrentUserAsync();
        return Page();
    }

    private async Task<IActionResult> GoNextAsync()
    {
        await PrepareAsync();

        if (!ValidateCurrentStep())
            return Page();

        if (Step < 6)
        {
            Step++;
            if (Step == 1)
                await PrefillFromCurrentUserAsync();
            if (Step == 2 && CategoryId <= 0 && Categories.Count > 0)
                CategoryId = Categories[0].Id;
            if (Step == 5)
                EnsureDefaultServices(force: ServiceNames.Count == 0 || AreDefaultServicePlaceholders());
            return Page();
        }

        return await SubmitAsync();
    }

    private bool AreDefaultServicePlaceholders()
    {
        // Re-localize canned defaults when the user changed language mid-wizard.
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Noche estándar", "Noche premium", "Standard night", "Premium night",
            "Consulta general", "Vacunas", "General checkup", "Vaccines",
            "Día completo", "Medio día", "Full day", "Half day",
            "Paseo 30 min", "Paseo 60 min", "30-min walk", "60-min walk",
            "Obediencia básica", "Sesión avanzada", "Basic obedience", "Advanced session",
            "Baño básico", "Corte de pelo", "Grooming completo", "Basic bath", "Haircut", "Full grooming"
        };
        return ServiceNames.Count > 0 && ServiceNames.All(n => known.Contains(n?.Trim() ?? ""));
    }

    private async Task PrepareAsync()
    {
        Categories = await _db.Categories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync();
        EnsureWeek();
        EnsureDefaultServices();
        if (CategoryId <= 0 && Categories.Count > 0 && Step >= 2)
            CategoryId = Categories[0].Id;
    }

    /// <summary>Prefills contact fields from the signed-in client converting to a business.</summary>
    private async Task PrefillFromCurrentUserAsync()
    {
        if (_auth.CurrentUserId is not int userId) return;
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return;

        if (string.IsNullOrWhiteSpace(FullName)) FullName = user.FullName;
        if (string.IsNullOrWhiteSpace(Email)) Email = user.Email;
        if (string.IsNullOrWhiteSpace(Phone)) Phone = user.Phone ?? "";
        if (!string.IsNullOrWhiteSpace(user.City)
            && (string.IsNullOrWhiteSpace(City) || City == "Charlotte, NC"))
            City = user.City;
        if (Latitude == 0 && Longitude == 0 && user.Latitude is double lat && user.Longitude is double lng)
        {
            Latitude = lat;
            Longitude = lng;
        }
    }

    /// <summary>Si no eligió de Maps, usa coordenadas por defecto para no bloquear el flujo. Never touches City.</summary>
    private void EnsureCoordsFallback()
    {
        if (Latitude == 0 && Longitude == 0)
        {
            Latitude = _maps.DefaultLatitude;
            Longitude = _maps.DefaultLongitude;
        }
    }

    private bool ValidateCurrentStep()
    {
        ErrorMessage = null;
        switch (Step)
        {
            case 0:
                if (ProviderKindKey is not ("business" or "independent"))
                {
                    ErrorMessage = _L["Biz_ErrChooseType"].Value;
                    return false;
                }
                break;
            case 1:
                if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(BusinessName)
                    || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(City))
                {
                    ErrorMessage = _L["Biz_ErrBasicRequired"].Value;
                    return false;
                }
                if (!new EmailAddressAttribute().IsValid(Email))
                {
                    ErrorMessage = _L["Profile_Edit_EmailInvalid"].Value;
                    return false;
                }
                if (string.IsNullOrWhiteSpace(Phone))
                {
                    ErrorMessage = _L["Phone_Required"].Value;
                    return false;
                }
                if (!PhoneValidator.TryNormalize(Phone, out var phoneNorm, required: true))
                {
                    ErrorMessage = _L["Phone_Invalid"].Value;
                    return false;
                }
                Phone = phoneNorm!;
                break;
            case 2:
                if (CategoryId <= 0)
                {
                    if (Categories.Count > 0)
                        CategoryId = Categories[0].Id;
                    else
                    {
                        ErrorMessage = _L["Biz_ErrNoCategories"].Value;
                        return false;
                    }
                }
                break;
            case 3:
                EnsureWeek();
                if (!Week.Any(d => d.IsOpen))
                {
                    ErrorMessage = _L["Biz_ErrOpenDay"].Value;
                    return false;
                }
                break;
            case 4:
                if (string.IsNullOrWhiteSpace(About) || About.Trim().Length < 20)
                {
                    ErrorMessage = _L["Biz_ErrDescription"].Value;
                    return false;
                }
                break;
            case 5:
                NormalizeServices();
                if (ServiceNames.Count == 0 || ServicePrices.All(p => p <= 0))
                {
                    ErrorMessage = _L["Biz_ErrServicePrice"].Value;
                    return false;
                }
                break;
            case 6:
                if (!_auth.IsAuthenticated)
                {
                    if (string.IsNullOrWhiteSpace(Password) || !PasswordPolicy.IsValid(Password))
                    {
                        ErrorMessage = _L["Profile_Edit_PasswordShort"].Value;
                        return false;
                    }
                    if (Password != ConfirmPassword)
                    {
                        ErrorMessage = _L["Biz_ErrPasswordMatch"].Value;
                        return false;
                    }
                }
                if (!AcceptTerms)
                {
                    ErrorMessage = _L["Biz_ErrAcceptTerms"].Value;
                    return false;
                }
                break;
        }
        return true;
    }

    private async Task<IActionResult> SubmitAsync()
    {
        EnsureCoordsFallback();
        var email = Email.Trim().ToLowerInvariant();
        AppUser user;

        if (_auth.CurrentUserId is int currentUserId)
        {
            var existing = await _db.Users
                .Include(u => u.GroomerProfile)
                .FirstOrDefaultAsync(u => u.Id == currentUserId);
            if (existing == null)
            {
                ErrorMessage = _L["Biz_ErrInvalidSession"].Value;
                Step = 0;
                return Page();
            }
            if (existing.GroomerProfile != null)
            {
                ErrorMessage = _L["Biz_ErrAlreadyBusiness"].Value;
                Step = 0;
                return Page();
            }
            if (await _db.Users.AnyAsync(u => u.Email == email && u.Id != existing.Id))
            {
                ErrorMessage = _L["Biz_ErrEmailTaken"].Value;
                Step = 1;
                return Page();
            }

            existing.FullName = FullName.Trim();
            existing.Email = email;
            existing.Phone = Phone;
            existing.City = City.Trim();
            existing.Role = UserRole.Groomer;
            user = existing;
        }
        else
        {
            if (await _db.Users.AnyAsync(u => u.Email == email))
            {
                ErrorMessage = _L["Biz_ErrEmailTakenLogin"].Value;
                Step = 1;
                return Page();
            }

            user = new AppUser
            {
                FullName = FullName.Trim(),
                Email = email,
                PasswordHash = PasswordHasher.Hash(Password),
                Phone = Phone,
                City = City.Trim(),
                Role = UserRole.Groomer
            };
            _db.Users.Add(user);
        }

        var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Id == CategoryId);
        if (cat == null)
        {
            ErrorMessage = _L["Biz_ErrInvalidCategory"].Value;
            Step = 2;
            return Page();
        }

        NormalizeServices();
        var work = WorkModeKey switch
        {
            "domicilio" => WorkMode.Mobile,
            "ambas" => WorkMode.Both,
            _ => WorkMode.Local
        };
        var kind = ProviderKindKey == "independent" ? ProviderKind.Independent : ProviderKind.Business;
        var groomerType = work switch
        {
            WorkMode.Mobile => GroomerType.Mobile,
            WorkMode.Both => GroomerType.InHome,
            _ => GroomerType.Salon
        };

        await _db.SaveChangesAsync();

        var firstPrice = ServicePrices.FirstOrDefault(p => p > 0);
        if (firstPrice <= 0) firstPrice = 35;
        var unit = cat.IsOvernight ? "/ noche" : "/ sesión";

        var profile = new GroomerProfile
        {
            UserId = user.Id,
            CategoryId = cat.Id,
            BusinessName = BusinessName.Trim(),
            ProviderKind = kind,
            WorkMode = work,
            Type = groomerType,
            ServiceAreaMiles = ServiceAreaMiles,
            Address = string.IsNullOrWhiteSpace(Address) ? City.Trim() : Address.Trim(),
            City = City.Trim(),
            Latitude = Latitude,
            Longitude = Longitude,
            Phone = Phone,
            About = About.Trim(),
            LogoUrl = string.IsNullOrWhiteSpace(LogoUrl) ? null : LogoUrl.Trim(),
            CoverUrl = string.IsNullOrWhiteSpace(CoverUrl) ? null : CoverUrl.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(CoverUrl) ? $"/images/categories/cat-{cat.Slug}-v2.png" : CoverUrl.Trim(),
            StartingPrice = firstPrice,
            PriceUnit = unit,
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
        _db.Groomers.Add(profile);
        await _db.SaveChangesAsync();

        for (var i = 0; i < ServiceNames.Count; i++)
        {
            var name = ServiceNames[i].Trim();
            var price = i < ServicePrices.Count ? ServicePrices[i] : firstPrice;
            if (string.IsNullOrWhiteSpace(name) || price <= 0) continue;
            _db.Services.Add(new GroomerService
            {
                GroomerId = profile.Id,
                Name = name,
                Description = $"Servicio de {cat.Name}",
                BillingUnit = cat.IsOvernight ? "noche" : "sesion",
                PriceSmall = price,
                PriceMedium = price + 10,
                PriceLarge = price + 20,
                PriceGiant = price + 30,
                DurationMinutes = cat.IsOvernight ? 1440 : 60
            });
        }
        await _db.SaveChangesAsync();
        await _availability.SaveWeeklyAndGenerateAsync(profile.Id, Week, days: 60);

        _db.Notifications.Add(new AppNotification
        {
            UserId = user.Id,
            Title = "¡Bienvenido a Chombly!",
            Message = "Tu perfil fue creado. Completa la verificación para publicar más rápido.",
            Type = "business"
        });

        var admins = await _db.Users.Where(u => u.Role == UserRole.Admin).Select(u => u.Id).ToListAsync();
        foreach (var adminId in admins)
        {
            _db.Notifications.Add(new AppNotification
            {
                UserId = adminId,
                Title = "Nuevo negocio pendiente",
                Message = $"{profile.BusinessName} ({cat.Name}) · {kind}",
                Type = "business"
            });
        }
        await _db.SaveChangesAsync();

        await _email.SendAsync(user.Email, "Chombly: perfil de negocio creado",
            $"<p>Hola {user.FullName},</p><p>¡Bienvenido! Creamos el perfil de <strong>{profile.BusinessName}</strong>.</p><p>— Equipo Chombly</p>");

        var adminTo = string.IsNullOrWhiteSpace(_smtp.AdminNotifyEmail) ? _smtp.From : _smtp.AdminNotifyEmail;
        await _email.SendAsync(adminTo, $"Chombly: nuevo negocio — {profile.BusinessName}",
            $"<p><strong>{profile.BusinessName}</strong> ({cat.Name}) · {user.Email} · {Phone}</p><p>{City}</p>");

        // Re-sign so Role claim becomes Groomer (needed when converting an existing client).
        await _auth.SignInAsync(user);
        Step = 7;
        return Page();
    }

    private void EnsureWeek()
    {
        if (Week == null || Week.Count != 7)
            Week = WeekDayInput.DefaultWeek();
        var names = new[]
        {
            _L["Day_Sunday"].Value,
            _L["Day_Monday"].Value,
            _L["Day_Tuesday"].Value,
            _L["Day_Wednesday"].Value,
            _L["Day_Thursday"].Value,
            _L["Day_Friday"].Value,
            _L["Day_Saturday"].Value
        };
        for (var i = 0; i < Week.Count && i < 7; i++)
        {
            Week[i].DayOfWeek = i;
            Week[i].Label = names[i];
            if (string.IsNullOrEmpty(Week[i].OpenTime)) Week[i].OpenTime = "08:00";
            if (string.IsNullOrEmpty(Week[i].CloseTime)) Week[i].CloseTime = "18:00";
        }
    }

    private void EnsureDefaultServices(bool force = false)
    {
        if (!force && ServiceNames.Count > 0) return;
        var cat = Categories.FirstOrDefault(c => c.Id == CategoryId);
        var slug = cat?.Slug ?? "grooming";
        var en = CultureCookie.IsEnglish();
        (ServiceNames, ServicePrices) = slug switch
        {
            "hotel" => en
                ? (new List<string> { "Standard night", "Premium night" }, new List<decimal> { 45, 60 })
                : (new List<string> { "Noche estándar", "Noche premium" }, new List<decimal> { 45, 60 }),
            "vet" => en
                ? (new List<string> { "General checkup", "Vaccines" }, new List<decimal> { 50, 35 })
                : (new List<string> { "Consulta general", "Vacunas" }, new List<decimal> { 50, 35 }),
            "daycare" => en
                ? (new List<string> { "Full day", "Half day" }, new List<decimal> { 35, 25 })
                : (new List<string> { "Día completo", "Medio día" }, new List<decimal> { 35, 25 }),
            "walkers" => en
                ? (new List<string> { "30-min walk", "60-min walk" }, new List<decimal> { 15, 25 })
                : (new List<string> { "Paseo 30 min", "Paseo 60 min" }, new List<decimal> { 15, 25 }),
            "trainers" => en
                ? (new List<string> { "Basic obedience", "Advanced session" }, new List<decimal> { 45, 60 })
                : (new List<string> { "Obediencia básica", "Sesión avanzada" }, new List<decimal> { 45, 60 }),
            _ => en
                ? (new List<string> { "Basic bath", "Haircut", "Full grooming" }, new List<decimal> { 35, 45, 55 })
                : (new List<string> { "Baño básico", "Corte de pelo", "Grooming completo" }, new List<decimal> { 35, 45, 55 })
        };
    }

    private void NormalizeServices()
    {
        var names = new List<string>();
        var prices = new List<decimal>();
        var n = Math.Max(ServiceNames?.Count ?? 0, ServicePrices?.Count ?? 0);
        for (var i = 0; i < n; i++)
        {
            var name = i < ServiceNames!.Count ? ServiceNames[i]?.Trim() ?? "" : "";
            var price = i < ServicePrices!.Count ? ServicePrices[i] : 0;
            if (string.IsNullOrWhiteSpace(name)) continue;
            names.Add(name);
            prices.Add(price);
        }
        ServiceNames = names;
        ServicePrices = prices;
    }
}
