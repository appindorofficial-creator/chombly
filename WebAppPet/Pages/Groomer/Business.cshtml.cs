using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Application.Businesses.AddAmenity;
using WebAppPet.Application.Businesses.AddExtra;
using WebAppPet.Application.Businesses.AddService;
using WebAppPet.Application.Businesses.Shared;
using WebAppPet.Application.Businesses.UpdateBusiness;
using WebAppPet.Data;
using WebAppPet.Infrastructure.Web;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Groomer;

public class BusinessModel : GroomerPageModel
{
    private readonly UpdateBusinessHandler _updateBusiness;
    private readonly AddAmenityHandler _addAmenity;
    private readonly AddExtraHandler _addExtra;
    private readonly AddServiceHandler _addService;
    private readonly IStringLocalizer<SharedResource> _L;

    public BusinessModel(
        AppDbContext db,
        AuthService auth,
        UpdateBusinessHandler updateBusiness,
        AddAmenityHandler addAmenity,
        AddExtraHandler addExtra,
        AddServiceHandler addService,
        IStringLocalizer<SharedResource> L)
        : base(db, auth)
    {
        _updateBusiness = updateBusiness;
        _addAmenity = addAmenity;
        _addExtra = addExtra;
        _addService = addService;
        _L = L;
    }

    public List<ServiceCategory> Categories { get; set; } = new();
    public List<BusinessAmenity> Amenities { get; set; } = new();
    public List<ServiceExtra> Extras { get; set; } = new();
    public List<GroomerService> Services { get; set; } = new();
    public string? Message { get; set; }

    [BindProperty] public string BusinessName { get; set; } = "";
    [BindProperty] public int? CategoryId { get; set; }
    [BindProperty] public List<int> CategoryIds { get; set; } = new();
    [BindProperty] public string Address { get; set; } = "";
    [BindProperty] public string City { get; set; } = "";
    [BindProperty, ModelBinder(typeof(InvariantCoordinateBinder))] public double Latitude { get; set; }
    [BindProperty, ModelBinder(typeof(InvariantCoordinateBinder))] public double Longitude { get; set; }
    [BindProperty] public string About { get; set; } = "";
    [BindProperty] public string? Phone { get; set; }
    [BindProperty] public string? ImageUrl { get; set; }
    [BindProperty] public string PriceUnit { get; set; } = "";
    [BindProperty] public decimal StartingPrice { get; set; }
    [BindProperty] public string AcceptedSpecies { get; set; } = PetSpecies.DefaultAcceptedList;
    [BindProperty] public bool AcceptsSeniorPets { get; set; }
    [BindProperty] public bool AcceptsAnxiousPets { get; set; }
    [BindProperty] public bool IsFeatured { get; set; }
    [BindProperty] public bool IsActive { get; set; } = true;

    [BindProperty] public string? NewAmenityLabel { get; set; }
    [BindProperty] public string NewAmenityIcon { get; set; } = "✓";
    [BindProperty] public string? NewExtraName { get; set; }
    [BindProperty] public decimal NewExtraPrice { get; set; }
    [BindProperty] public string? NewServiceName { get; set; }
    [BindProperty] public string NewServiceUnit { get; set; } = "sesion";
    [BindProperty] public decimal NewServicePrice { get; set; }

    /// <summary>Hotel-only: offer bookable “Cámara privada” extra.</summary>
    [BindProperty] public bool OffersPrivateCamera { get; set; }
    [BindProperty] public decimal PrivateCameraPrice { get; set; } = HotelPrivateCameraExtra.DefaultPrice;
    [BindProperty] public decimal ExtraBathPrice { get; set; } = HotelCoreExtras.BathDefaultPrice;
    [BindProperty] public decimal ExtraMedsPrice { get; set; } = HotelCoreExtras.MedsDefaultPrice;

    public bool IsHotelBusiness { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (await LoadGroomerAsync() is IActionResult r) return r;
        await FillAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (await LoadGroomerAsync() is IActionResult r) return r;

        var error = await _updateBusiness.HandleAsync(new UpdateBusinessCommand
        {
            BusinessId = Profile!.Id,
            BusinessName = BusinessName,
            CategoryId = CategoryId,
            CategoryIds = CategoryIds ?? [],
            Address = Address,
            City = City,
            Latitude = Latitude,
            Longitude = Longitude,
            About = About,
            Phone = Phone,
            ImageUrl = ImageUrl,
            PriceUnit = PriceUnit,
            StartingPrice = StartingPrice,
            AcceptedSpecies = AcceptedSpecies,
            AcceptsSeniorPets = AcceptsSeniorPets,
            AcceptsAnxiousPets = AcceptsAnxiousPets,
            IsFeatured = IsFeatured,
            IsActive = IsActive,
            HotelExtras = new HotelExtraPrices(ExtraBathPrice, ExtraMedsPrice, OffersPrivateCamera, PrivateCameraPrice)
        });

        Message = error switch
        {
            UpdateBusinessError.NoCategory => _L["BizPanel_ErrCategory"].Value,
            UpdateBusinessError.PhoneInvalid => _L["Phone_Invalid"].Value,
            _ => _L["BizPanel_Updated"].Value
        };
        await FillAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAddAmenityAsync()
    {
        if (await LoadGroomerAsync() is IActionResult r) return r;
        await _addAmenity.HandleAsync(new AddAmenityCommand(Profile!.Id, NewAmenityLabel, NewAmenityIcon));
        return RedirectToPage((string?)null, (string?)null, "biz-amenities");
    }

    public async Task<IActionResult> OnPostAddExtraAsync()
    {
        if (await LoadGroomerAsync() is IActionResult r) return r;
        if (!string.IsNullOrWhiteSpace(NewExtraName) && HotelCoreExtras.IsManagedHotelExtra(NewExtraName.Trim()))
            return RedirectToPage((string?)null, (string?)null, "biz-hotel-extras");

        await _addExtra.HandleAsync(new AddExtraCommand(Profile!.Id, NewExtraName, NewExtraPrice));
        return RedirectToPage((string?)null, (string?)null, "biz-extras");
    }

    public async Task<IActionResult> OnPostAddServiceAsync()
    {
        if (await LoadGroomerAsync() is IActionResult r) return r;
        await _addService.HandleAsync(new AddServiceCommand(
            Profile!.Id, NewServiceName, NewServiceUnit, NewServicePrice,
            Profile.User?.CountryCode ?? Profile.LicenseCountry));
        return RedirectToPage((string?)null, (string?)null, "biz-services");
    }

    private async Task FillAsync()
    {
        Categories = await Db.Categories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync();
        Profile = await Db.Groomers.Include(g => g.Category).Include(g => g.User).FirstAsync(g => g.Id == Profile!.Id);
        await LoadOfferedCategoriesAsync();
        var g = Profile;
        BusinessName = g.BusinessName;
        CategoryId = g.CategoryId;
        CategoryIds = g.GetOfferedCategoryIds().ToList();
        Address = g.Address;
        City = g.City;
        var lat = g.Latitude;
        var lng = g.Longitude;
        if (GeoHelper.TryRepairCoordinates(ref lat, ref lng))
        {
            g.Latitude = lat;
            g.Longitude = lng;
            await Db.SaveChangesAsync();
        }
        Latitude = g.Latitude;
        Longitude = g.Longitude;
        About = g.About;
        Phone = g.Phone;
        ImageUrl = g.ImageUrl;
        PriceUnit = g.PriceUnit ?? "";
        StartingPrice = g.StartingPrice;
        AcceptedSpecies = g.AcceptedSpecies;
        AcceptsSeniorPets = g.AcceptsSeniorPets;
        AcceptsAnxiousPets = g.AcceptsAnxiousPets;
        IsFeatured = g.IsFeatured;
        IsActive = g.IsActive;
        Amenities = await Db.Amenities.Where(a => a.GroomerId == g.Id).OrderBy(a => a.SortOrder).ToListAsync();
        Extras = await Db.ServiceExtras.Where(e => e.GroomerId == g.Id).ToListAsync();
        Services = await Db.Services.Where(s => s.GroomerId == g.Id).ToListAsync();
        IsHotelBusiness = IsHotelFromCategories(CategoryIds)
            || string.Equals(g.Category?.Slug, "hotel", StringComparison.OrdinalIgnoreCase);
        if (IsHotelBusiness)
        {
            var cam = await HotelPrivateCameraExtra.GetAsync(Db, g.Id, g.User?.CountryCode ?? g.LicenseCountry);
            OffersPrivateCamera = cam.Enabled;
            PrivateCameraPrice = cam.Price;
            var core = await HotelCoreExtras.GetPricesAsync(Db, g.Id, g.User?.CountryCode ?? g.LicenseCountry);
            ExtraBathPrice = core.BathPrice;
            ExtraMedsPrice = core.MedsPrice;
        }
    }

    private bool IsHotelFromCategories(IEnumerable<int> categoryIds)
    {
        var ids = categoryIds?.ToHashSet() ?? new HashSet<int>();
        return Categories.Any(c => ids.Contains(c.Id)
            && string.Equals(c.Slug, "hotel", StringComparison.OrdinalIgnoreCase));
    }
}
