using System.ComponentModel.DataAnnotations;
using System.Text.Json;
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
    private readonly IWebHostEnvironment _env;

    public RegisterBusinessModel(
        AppDbContext db,
        AuthService auth,
        AvailabilityService availability,
        IEmailService email,
        IOptions<SmtpOptions> smtp,
        IOptions<GoogleMapsOptions> maps,
        IStringLocalizer<SharedResource> L,
        IWebHostEnvironment env)
    {
        _db = db;
        _auth = auth;
        _availability = availability;
        _email = email;
        _smtp = smtp.Value;
        _maps = maps.Value;
        _L = L;
        _env = env;
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
    [BindProperty] public List<int> CategoryIds { get; set; } = new();
    [BindProperty] public string WorkModeKey { get; set; } = "local";
    [BindProperty] public List<WeekDayInput> Week { get; set; } = WeekDayInput.DefaultWeek();
    [BindProperty] public int ServiceAreaMiles { get; set; } = 10;
    [BindProperty, MaxLength(800)] public string About { get; set; } = string.Empty;
    [BindProperty] public string? LogoUrl { get; set; }
    [BindProperty] public string? CoverUrl { get; set; }
    [BindProperty] public IFormFile? LogoFile { get; set; }
    [BindProperty] public IFormFile? CoverFile { get; set; }
    [BindProperty] public List<string> ServiceNames { get; set; } = new();
    [BindProperty] public List<decimal> ServicePrices { get; set; } = new();
    [BindProperty] public string Password { get; set; } = string.Empty;
    [BindProperty] public string ConfirmPassword { get; set; } = string.Empty;
    [BindProperty] public bool AcceptTerms { get; set; }

    /// <summary>Hotel category: offer bookable private-camera extra.</summary>
    [BindProperty] public bool OffersPrivateCamera { get; set; }
    [BindProperty] public decimal PrivateCameraPrice { get; set; } = HotelPrivateCameraExtra.DefaultPrice;
    [BindProperty] public decimal ExtraBathPrice { get; set; } = HotelCoreExtras.BathDefaultPrice;
    [BindProperty] public decimal ExtraMedsPrice { get; set; } = HotelCoreExtras.MedsDefaultPrice;

    public bool IsHotelSelected =>
        Categories.Any(c => CategoryIds.Contains(c.Id)
            && string.Equals(c.Slug, "hotel", StringComparison.OrdinalIgnoreCase));

    /// <summary>Market inferred from step-1 city / GPS (US → mi, Colombia → km).</summary>
    public BusinessMarket DetectedMarket =>
        BusinessMarketResolver.ResolveUser(
            City,
            Latitude == 0 && Longitude == 0 ? null : Latitude,
            Latitude == 0 && Longitude == 0 ? null : Longitude);

    public bool ServiceAreaUsesKm => DetectedMarket == BusinessMarket.Colombia;

    public string FormatServiceAreaLabel(int value)
    {
        if (value <= 0) return _L["Biz_WholeCity"].Value;
        return ServiceAreaUsesKm
            ? CatalogLocalizer.Loc($"{value} km", $"{value} km")
            : CatalogLocalizer.Loc($"{value} mi", $"{value} mi");
    }

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
        var stepFromQuery = Request.Query.ContainsKey(nameof(Step));
        var queryStep = Step;
        TryRestoreDraft();
        if (stepFromQuery)
            Step = queryStep;

        if (Step is < 0 or > 7) Step = 0;
        // Account step is only for guests; signed-in users finish on prices + terms.
        if (Step == 6 && _auth.IsAuthenticated)
            Step = 5;
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
        return WizardPage();
    }

    private async Task<IActionResult> GoNextAsync()
    {
        await PrepareAsync();

        if (Step == 4)
        {
            var uploadErr = await TrySavePhotoUploadsAsync();
            if (uploadErr != null)
            {
                ErrorMessage = uploadErr;
                return WizardPage();
            }
        }

        if (!ValidateCurrentStep())
            return WizardPage();

        // Logged-in users already have an account — don't send them to "create account".
        if (Step == 5 && _auth.IsAuthenticated)
            return await SubmitAsync();

        if (Step < 6)
        {
            Step++;
            if (Step == 1)
                await PrefillFromCurrentUserAsync();
            if (Step == 2 && CategoryIds.Count == 0 && Categories.Count > 0)
            {
                CategoryIds = new List<int> { Categories[0].Id };
                CategoryId = Categories[0].Id;
            }
            if (Step == 5)
                EnsureDefaultServices(force: ServiceNames.Count == 0 || AreDefaultServicePlaceholders());
            // Guests never land on step 6 while authenticated; if URL forced it, bounce.
            if (Step == 6 && _auth.IsAuthenticated)
                return await SubmitAsync();
            return WizardPage();
        }

        return await SubmitAsync();
    }

    private IActionResult WizardPage()
    {
        SaveDraft();
        return Page();
    }

    private async Task<string?> TrySavePhotoUploadsAsync()
    {
        var logoErr = BusinessPhotoStorage.Validate(LogoFile);
        if (logoErr != null)
            return _L["Biz_PhotoInvalid"].Value;

        var coverErr = BusinessPhotoStorage.Validate(CoverFile);
        if (coverErr != null)
            return _L["Biz_PhotoInvalid"].Value;

        var uid = _auth.CurrentUserId;
        try
        {
            if (LogoFile is { Length: > 0 })
                LogoUrl = await BusinessPhotoStorage.SaveAsync(LogoFile, uid, _env);
            if (CoverFile is { Length: > 0 })
                CoverUrl = await BusinessPhotoStorage.SaveAsync(CoverFile, uid, _env);
        }
        catch
        {
            return _L["Biz_PhotoInvalid"].Value;
        }

        return null;
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
        SyncPrimaryCategoryId();
        if (CategoryIds.Count == 0 && CategoryId > 0)
            CategoryIds = new List<int> { CategoryId };
        if (CategoryIds.Count == 0 && Categories.Count > 0 && Step >= 2)
        {
            CategoryIds = new List<int> { Categories[0].Id };
            CategoryId = Categories[0].Id;
        }
    }

    private void SyncPrimaryCategoryId()
    {
        CategoryIds = (CategoryIds ?? new List<int>())
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        if (CategoryIds.Count > 0)
            CategoryId = CategoryIds[0];
        else if (CategoryId > 0)
            CategoryIds = new List<int> { CategoryId };
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
                FullName = (FullName ?? "").Trim();
                BusinessName = (BusinessName ?? "").Trim();
                Email = (Email ?? "").Trim();
                City = (City ?? "").Trim();
                Phone = (Phone ?? "").Trim();

                if (string.IsNullOrWhiteSpace(FullName))
                {
                    ErrorMessage = _L["Profile_Edit_NameRequired"].Value;
                    return false;
                }
                if (string.IsNullOrWhiteSpace(BusinessName))
                {
                    ErrorMessage = _L["Biz_ErrBusinessNameRequired"].Value;
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
                if (string.IsNullOrWhiteSpace(Email) || !new EmailAddressAttribute().IsValid(Email))
                {
                    ErrorMessage = _L["Profile_Edit_EmailInvalid"].Value;
                    return false;
                }
                if (string.IsNullOrWhiteSpace(City) || (Latitude == 0 && Longitude == 0))
                {
                    ErrorMessage = _L["Register_LocationRequired"].Value;
                    return false;
                }
                break;
            case 2:
                SyncPrimaryCategoryId();
                if (CategoryIds.Count == 0)
                {
                    ErrorMessage = _L["Biz_ErrPickService"].Value;
                    return false;
                }
                if (!CategoryIds.All(id => Categories.Any(c => c.Id == id)))
                {
                    ErrorMessage = _L["Biz_ErrPickService"].Value;
                    return false;
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
                if (_auth.IsAuthenticated && !AcceptTerms)
                {
                    ErrorMessage = _L["Biz_ErrAcceptTerms"].Value;
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
                return WizardPage();
            }
            if (existing.GroomerProfile != null)
            {
                ErrorMessage = _L["Biz_ErrAlreadyBusiness"].Value;
                Step = 0;
                return WizardPage();
            }
            if (await _db.Users.AnyAsync(u => u.Email == email && u.Id != existing.Id))
            {
                ErrorMessage = _L["Biz_ErrEmailTaken"].Value;
                Step = 1;
                return WizardPage();
            }

            existing.FullName = FullName.Trim();
            existing.Email = email;
            existing.Phone = Phone;
            existing.City = City.Trim();
            existing.Latitude = Latitude == 0 ? existing.Latitude : Latitude;
            existing.Longitude = Longitude == 0 ? existing.Longitude : Longitude;
            if (Latitude != 0 && Longitude != 0)
                existing.LocationUpdatedAt = DateTime.UtcNow;
            MarketCountry.ApplyFromLocation(existing, City, Latitude == 0 ? null : Latitude, Longitude == 0 ? null : Longitude);
            existing.Role = UserRole.Groomer;
            user = existing;
        }
        else
        {
            if (await _db.Users.AnyAsync(u => u.Email == email))
            {
                ErrorMessage = _L["Biz_ErrEmailTakenLogin"].Value;
                Step = 1;
                return WizardPage();
            }

            user = new AppUser
            {
                FullName = FullName.Trim(),
                Email = email,
                PasswordHash = PasswordHasher.Hash(Password),
                Phone = Phone,
                City = City.Trim(),
                Latitude = Latitude == 0 ? null : Latitude,
                Longitude = Longitude == 0 ? null : Longitude,
                LocationUpdatedAt = Latitude == 0 && Longitude == 0 ? null : DateTime.UtcNow,
                Role = UserRole.Groomer
            };
            MarketCountry.ApplyFromLocation(user);
            _db.Users.Add(user);
        }

        await _db.SaveChangesAsync();

        SyncPrimaryCategoryId();
        var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Id == CategoryId);
        if (cat == null)
        {
            ErrorMessage = _L["Biz_ErrInvalidCategory"].Value;
            Step = 2;
            return WizardPage();
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

        var firstPrice = ServicePrices.FirstOrDefault(p => p > 0);
        if (firstPrice <= 0) firstPrice = 35;
        var unit = cat.IsOvernight
            ? CatalogLocalizer.Loc("/ noche", "/ night")
            : CatalogLocalizer.Loc("/ sesión", "/ session");
        var catLabel = cat.DisplayName();
        var serviceDescPrefix = CatalogLocalizer.Loc("Servicio de", "Service:");

        var profile = new GroomerProfile
        {
            UserId = user.Id,
            CategoryId = cat.Id,
            ExtraCategoryIds = GroomerProfile.JoinExtraCategoryIds(CategoryIds, cat.Id),
            BusinessName = BusinessName.Trim(),
            ProviderKind = kind,
            WorkMode = work,
            Type = groomerType,
            ServiceAreaMiles = ToStoredServiceAreaMiles(ServiceAreaMiles),
            LicenseCountry = MarketCountry.Normalize(
                DetectedMarket == BusinessMarket.Unknown
                    ? user.CountryCode
                    : MarketCountry.FromMarket(DetectedMarket)),
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
                Description = $"{serviceDescPrefix} {catLabel}",
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

        if (IsHotelSelected)
        {
            await HotelCoreExtras.SyncAsync(_db, profile.Id, ExtraBathPrice, ExtraMedsPrice);
            if (OffersPrivateCamera)
                await HotelPrivateCameraExtra.SyncAsync(_db, profile.Id, true, PrivateCameraPrice);
        }

        _db.Notifications.Add(new AppNotification
        {
            UserId = user.Id,
            // Stored in Spanish; NotificationLocalizer translates on read.
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

        await _email.SendAsync(
            user.Email,
            CatalogLocalizer.Loc("Chombly: perfil de negocio creado", "Chombly: business profile created"),
            CatalogLocalizer.Loc(
                $"<p>Hola {user.FullName},</p><p>¡Bienvenido! Creamos el perfil de <strong>{profile.BusinessName}</strong>.</p><p>— Equipo Chombly</p>",
                $"<p>Hi {user.FullName},</p><p>Welcome! We created the profile for <strong>{profile.BusinessName}</strong>.</p><p>— Chombly team</p>"));

        var adminTo = string.IsNullOrWhiteSpace(_smtp.AdminNotifyEmail) ? _smtp.From : _smtp.AdminNotifyEmail;
        await _email.SendAsync(
            adminTo,
            CatalogLocalizer.Loc(
                $"Chombly: nuevo negocio — {profile.BusinessName}",
                $"Chombly: new business — {profile.BusinessName}"),
            $"<p><strong>{profile.BusinessName}</strong> ({catLabel}) · {user.Email} · {Phone}</p><p>{City}</p>");

        // Re-sign so Role claim becomes Groomer (needed when converting an existing client).
        await _auth.SignInAsync(user);
        _auth.SetShellMode(AppShellMode.Business);
        ClearDraft();
        Step = 7;
        return Page();
    }

    private int ToStoredServiceAreaMiles(int selected)
    {
        if (selected <= 0) return 0;
        // Form chips are round local units (km in Colombia, mi in US/other).
        if (!ServiceAreaUsesKm) return selected;
        return Math.Max(1, (int)Math.Round(selected / 1.609344));
    }

    private const string DraftSessionKey = "RegisterBusiness.Draft";

    private void SaveDraft()
    {
        if (Step is < 0 or > 6) return;
        try
        {
            var draft = new BizRegDraft
            {
                Step = Step,
                ProviderKindKey = ProviderKindKey,
                FullName = FullName,
                BusinessName = BusinessName,
                Phone = Phone,
                Email = Email,
                City = City,
                Address = Address,
                Latitude = Latitude,
                Longitude = Longitude,
                CategoryId = CategoryId,
                CategoryIds = CategoryIds?.ToList() ?? new(),
                WorkModeKey = WorkModeKey,
                Week = Week?.Select(w => new BizRegWeekDay
                {
                    DayOfWeek = w.DayOfWeek,
                    Label = w.Label,
                    IsOpen = w.IsOpen,
                    OpenTime = w.OpenTime,
                    CloseTime = w.CloseTime
                }).ToList() ?? new(),
                ServiceAreaMiles = ServiceAreaMiles,
                About = About,
                LogoUrl = LogoUrl,
                CoverUrl = CoverUrl,
                ServiceNames = ServiceNames?.ToList() ?? new(),
                ServicePrices = ServicePrices?.ToList() ?? new(),
                AcceptTerms = AcceptTerms,
                OffersPrivateCamera = OffersPrivateCamera,
                PrivateCameraPrice = PrivateCameraPrice,
                ExtraBathPrice = ExtraBathPrice,
                ExtraMedsPrice = ExtraMedsPrice
            };
            HttpContext.Session.SetString(DraftSessionKey, JsonSerializer.Serialize(draft));
        }
        catch
        {
            // Session may be unavailable; wizard still works via POST hiddens.
        }
    }

    private void TryRestoreDraft()
    {
        try
        {
            var json = HttpContext.Session.GetString(DraftSessionKey);
            if (string.IsNullOrWhiteSpace(json)) return;
            var draft = JsonSerializer.Deserialize<BizRegDraft>(json);
            if (draft == null) return;

            Step = draft.Step;
            ProviderKindKey = draft.ProviderKindKey ?? ProviderKindKey;
            FullName = draft.FullName ?? "";
            BusinessName = draft.BusinessName ?? "";
            Phone = draft.Phone ?? "";
            Email = draft.Email ?? "";
            City = draft.City ?? "";
            Address = draft.Address ?? "";
            Latitude = draft.Latitude;
            Longitude = draft.Longitude;
            CategoryId = draft.CategoryId;
            CategoryIds = draft.CategoryIds ?? new();
            WorkModeKey = draft.WorkModeKey ?? WorkModeKey;
            if (draft.Week is { Count: > 0 })
            {
                Week = draft.Week.Select(w => new WeekDayInput
                {
                    DayOfWeek = w.DayOfWeek,
                    Label = w.Label ?? "",
                    IsOpen = w.IsOpen,
                    OpenTime = w.OpenTime ?? "08:00",
                    CloseTime = w.CloseTime ?? "18:00"
                }).ToList();
            }
            ServiceAreaMiles = draft.ServiceAreaMiles;
            About = draft.About ?? "";
            LogoUrl = draft.LogoUrl;
            CoverUrl = draft.CoverUrl;
            ServiceNames = draft.ServiceNames ?? new();
            ServicePrices = draft.ServicePrices ?? new();
            AcceptTerms = draft.AcceptTerms;
            OffersPrivateCamera = draft.OffersPrivateCamera;
            PrivateCameraPrice = draft.PrivateCameraPrice < 0
                ? HotelPrivateCameraExtra.DefaultPrice
                : draft.PrivateCameraPrice;
            ExtraBathPrice = draft.ExtraBathPrice < 0
                ? HotelCoreExtras.BathDefaultPrice
                : draft.ExtraBathPrice;
            ExtraMedsPrice = draft.ExtraMedsPrice < 0
                ? HotelCoreExtras.MedsDefaultPrice
                : draft.ExtraMedsPrice;
        }
        catch
        {
            ClearDraft();
        }
    }

    private void ClearDraft()
    {
        try { HttpContext.Session.Remove(DraftSessionKey); }
        catch { /* ignore */ }
    }

    private sealed class BizRegDraft
    {
        public int Step { get; set; }
        public string? ProviderKindKey { get; set; }
        public string? FullName { get; set; }
        public string? BusinessName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int CategoryId { get; set; }
        public List<int>? CategoryIds { get; set; }
        public string? WorkModeKey { get; set; }
        public List<BizRegWeekDay>? Week { get; set; }
        public int ServiceAreaMiles { get; set; }
        public string? About { get; set; }
        public string? LogoUrl { get; set; }
        public string? CoverUrl { get; set; }
        public List<string>? ServiceNames { get; set; }
        public List<decimal>? ServicePrices { get; set; }
        public bool AcceptTerms { get; set; }
        public bool OffersPrivateCamera { get; set; }
        public decimal PrivateCameraPrice { get; set; }
        public decimal ExtraBathPrice { get; set; }
        public decimal ExtraMedsPrice { get; set; }
    }

    private sealed class BizRegWeekDay
    {
        public int DayOfWeek { get; set; }
        public string? Label { get; set; }
        public bool IsOpen { get; set; }
        public string? OpenTime { get; set; }
        public string? CloseTime { get; set; }
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
        if (!force && ServiceNames.Count > 0)
        {
            NormalizeDefaultServiceNamesToSpanish();
            return;
        }
        SyncPrimaryCategoryId();
        var selected = Categories.Where(c => CategoryIds.Contains(c.Id)).ToList();
        if (selected.Count == 0)
        {
            var cat = Categories.FirstOrDefault(c => c.Id == CategoryId);
            if (cat != null) selected.Add(cat);
        }

        var names = new List<string>();
        var prices = new List<decimal>();
        // Canonical catalog language is Spanish; CatalogLocalizer.Text renders EN in the UI.
        foreach (var cat in selected)
        {
            var (n, p) = DefaultsForSlug(cat.Slug, en: false);
            for (var i = 0; i < n.Count; i++)
            {
                if (names.Contains(n[i], StringComparer.OrdinalIgnoreCase)) continue;
                names.Add(n[i]);
                prices.Add(p[i]);
            }
        }

        if (names.Count == 0)
        {
            var (n, p) = DefaultsForSlug("grooming", en: false);
            names = n;
            prices = p;
        }

        ServiceNames = names;
        ServicePrices = prices;
    }

    /// <summary>Keep canned defaults in Spanish so ES/EN UI can localize via CatalogLocalizer.</summary>
    private void NormalizeDefaultServiceNamesToSpanish()
    {
        if (!AreDefaultServicePlaceholders()) return;

        static string ToSpanish(string? name) => (name ?? "").Trim() switch
        {
            "Standard night" => "Noche estándar",
            "Premium night" => "Noche premium",
            "General checkup" => "Consulta general",
            "Vaccines" => "Vacunas",
            "Full day" => "Día completo",
            "Half day" => "Medio día",
            "30-min walk" => "Paseo 30 min",
            "60-min walk" => "Paseo 60 min",
            "Basic obedience" => "Obediencia básica",
            "Advanced session" => "Sesión avanzada",
            "Basic bath" => "Baño básico",
            "Haircut" => "Corte de pelo",
            "Full grooming" => "Grooming completo",
            var n => n
        };

        for (var i = 0; i < ServiceNames.Count; i++)
            ServiceNames[i] = ToSpanish(ServiceNames[i]);
    }

    private static (List<string> Names, List<decimal> Prices) DefaultsForSlug(string slug, bool en) => slug switch
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
