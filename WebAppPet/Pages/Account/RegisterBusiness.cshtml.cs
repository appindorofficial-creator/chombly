using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using WebAppPet.Application.Businesses.CreateBusiness;
using WebAppPet.Application.Businesses.Shared;
using WebAppPet.Data;
using WebAppPet.Infrastructure.Web;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Account;

public class RegisterBusinessModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly CreateBusinessHandler _createBusiness;
    private readonly GoogleMapsOptions _maps;
    private readonly IStringLocalizer<SharedResource> _L;
    private readonly IWebHostEnvironment _env;

    public RegisterBusinessModel(
        AppDbContext db,
        AuthService auth,
        CreateBusinessHandler createBusiness,
        IOptions<GoogleMapsOptions> maps,
        IStringLocalizer<SharedResource> L,
        IWebHostEnvironment env)
    {
        _db = db;
        _auth = auth;
        _createBusiness = createBusiness;
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
    [BindProperty, ModelBinder(typeof(InvariantCoordinateBinder))] public double Latitude { get; set; }
    [BindProperty, ModelBinder(typeof(InvariantCoordinateBinder))] public double Longitude { get; set; }
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
    [BindProperty] public decimal PrivateCameraPrice { get; set; }
    [BindProperty] public decimal ExtraBathPrice { get; set; }
    [BindProperty] public decimal ExtraMedsPrice { get; set; }

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
        ApplyMarketExtraDefaultsIfUnset();
    }

    private void ApplyMarketExtraDefaultsIfUnset()
    {
        var iso = DetectedMarket == BusinessMarket.Colombia
            ? "CO"
            : DetectedMarket == BusinessMarket.UnitedStates
                ? "US"
                : AppTimeZones.CurrentCountryCode;
        if (ExtraBathPrice <= 0) ExtraBathPrice = HotelCoreExtras.BathDefaultFor(iso);
        if (ExtraMedsPrice <= 0) ExtraMedsPrice = HotelCoreExtras.MedsDefaultFor(iso);
        if (PrivateCameraPrice <= 0) PrivateCameraPrice = HotelPrivateCameraExtra.DefaultFor(iso);
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
                EnsureDefaultServices(force: ServiceNames.Count == 0 || BusinessServiceDefaults.ArePlaceholders(ServiceNames));
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
        var error = RegistrationError.None;
        switch (Step)
        {
            case 0:
                error = BusinessRegistrationSteps.ValidateType(ProviderKindKey);
                break;
            case 1:
                (error, var basics) = BusinessRegistrationSteps.ValidateBasics(
                    new BusinessBasics(FullName, BusinessName, Phone, Email, City, Latitude, Longitude));
                FullName = basics.FullName;
                BusinessName = basics.BusinessName;
                Phone = basics.Phone;
                Email = basics.Email;
                City = basics.City;
                break;
            case 2:
                SyncPrimaryCategoryId();
                error = BusinessRegistrationSteps.ValidateCategories(CategoryIds, Categories);
                break;
            case 3:
                EnsureWeek();
                error = BusinessRegistrationSteps.ValidateSchedule(Week);
                break;
            case 4:
                error = BusinessRegistrationSteps.ValidateAbout(About);
                break;
            case 5:
                NormalizeServices();
                error = BusinessRegistrationSteps.ValidatePrices(ServiceNames, ServicePrices, _auth.IsAuthenticated, AcceptTerms);
                break;
            case 6:
                error = BusinessRegistrationSteps.ValidateAccount(_auth.IsAuthenticated, Password, ConfirmPassword, AcceptTerms);
                break;
        }

        if (error == RegistrationError.None)
            return true;
        ErrorMessage = ErrorText(error);
        return false;
    }

    private async Task<IActionResult> SubmitAsync()
    {
        EnsureCoordsFallback();
        SyncPrimaryCategoryId();

        var result = await _createBusiness.HandleAsync(new CreateBusinessCommand
        {
            CurrentUserId = _auth.CurrentUserId,
            ProviderKindKey = ProviderKindKey,
            FullName = FullName,
            BusinessName = BusinessName,
            Phone = Phone,
            Email = Email,
            City = City,
            Address = Address,
            Latitude = Latitude,
            Longitude = Longitude,
            PrimaryCategoryId = CategoryId,
            CategoryIds = CategoryIds,
            WorkModeKey = WorkModeKey,
            Week = Week,
            ServiceArea = ServiceAreaMiles,
            About = About,
            LogoUrl = LogoUrl,
            CoverUrl = CoverUrl,
            ServiceNames = ServiceNames,
            ServicePrices = ServicePrices,
            Password = Password,
            HotelExtras = new HotelExtraPrices(ExtraBathPrice, ExtraMedsPrice, OffersPrivateCamera, PrivateCameraPrice)
        });

        if (result.User is not { } user)
        {
            ErrorMessage = ErrorText(result.Error);
            Step = result.Error switch
            {
                RegistrationError.EmailTaken or RegistrationError.EmailTakenLogin => 1,
                RegistrationError.InvalidCategory => 2,
                _ => 0
            };
            return WizardPage();
        }

        // Re-sign so Role claim becomes Groomer (needed when converting an existing client).
        await _auth.SignInAsync(user);
        _auth.SetShellMode(AppShellMode.Business);
        ClearDraft();
        Step = 7;
        return Page();
    }

    private string ErrorText(RegistrationError error) => _L[error switch
    {
        RegistrationError.ChooseType => "Biz_ErrChooseType",
        RegistrationError.NameRequired => "Profile_Edit_NameRequired",
        RegistrationError.BusinessNameRequired => "Biz_ErrBusinessNameRequired",
        RegistrationError.PhoneRequired => "Phone_Required",
        RegistrationError.PhoneInvalid => "Phone_Invalid",
        RegistrationError.EmailInvalid => "Profile_Edit_EmailInvalid",
        RegistrationError.LocationRequired => "Register_LocationRequired",
        RegistrationError.PickService => "Biz_ErrPickService",
        RegistrationError.OpenDay => "Biz_ErrOpenDay",
        RegistrationError.Description => "Biz_ErrDescription",
        RegistrationError.ServicePrice => "Biz_ErrServicePrice",
        RegistrationError.AcceptTerms => "Biz_ErrAcceptTerms",
        RegistrationError.PasswordWeak => "Profile_Edit_PasswordShort",
        RegistrationError.PasswordMismatch => "Biz_ErrPasswordMatch",
        RegistrationError.InvalidSession => "Biz_ErrInvalidSession",
        RegistrationError.AlreadyBusiness => "Biz_ErrAlreadyBusiness",
        RegistrationError.EmailTaken => "Biz_ErrEmailTaken",
        RegistrationError.EmailTakenLogin => "Biz_ErrEmailTakenLogin",
        _ => "Biz_ErrInvalidCategory"
    }].Value;

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
            var draftIso = DetectedMarket == BusinessMarket.Colombia ? "CO"
                : DetectedMarket == BusinessMarket.UnitedStates ? "US"
                : AppTimeZones.CurrentCountryCode;
            PrivateCameraPrice = draft.PrivateCameraPrice < 0
                ? HotelPrivateCameraExtra.DefaultFor(draftIso)
                : draft.PrivateCameraPrice;
            ExtraBathPrice = draft.ExtraBathPrice < 0
                ? HotelCoreExtras.BathDefaultFor(draftIso)
                : draft.ExtraBathPrice;
            ExtraMedsPrice = draft.ExtraMedsPrice < 0
                ? HotelCoreExtras.MedsDefaultFor(draftIso)
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
            BusinessServiceDefaults.NormalizePlaceholdersToSpanish(ServiceNames);
            return;
        }
        SyncPrimaryCategoryId();
        var selected = Categories.Where(c => CategoryIds.Contains(c.Id)).ToList();
        if (selected.Count == 0 && Categories.FirstOrDefault(c => c.Id == CategoryId) is { } primary)
            selected.Add(primary);

        (ServiceNames, ServicePrices) = BusinessServiceDefaults.ForCategories(selected);
    }

    private void NormalizeServices() =>
        (ServiceNames, ServicePrices) = BusinessServiceDefaults.Normalize(ServiceNames, ServicePrices);
}
