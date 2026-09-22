using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Groomer;

public class BusinessModel : GroomerPageModel
{
    private readonly IStringLocalizer<SharedResource> _L;

    public BusinessModel(AppDbContext db, AuthService auth, IStringLocalizer<SharedResource> L)
        : base(db, auth)
    {
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
    [BindProperty] public double Latitude { get; set; }
    [BindProperty] public double Longitude { get; set; }
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
        var g = Profile!;
        Categories = await Db.Categories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync();

        var selected = (CategoryIds ?? new List<int>())
            .Where(id => id > 0 && Categories.Any(c => c.Id == id))
            .Distinct()
            .ToList();
        if (selected.Count == 0)
        {
            Message = _L["BizPanel_ErrCategory"].Value;
            await FillAsync();
            return Page();
        }

        var primary = CategoryId is int keep && selected.Contains(keep)
            ? keep
            : Categories.Where(c => selected.Contains(c.Id)).OrderBy(c => c.SortOrder).Select(c => c.Id).First();

        g.BusinessName = BusinessName.Trim();
        g.CategoryId = primary;
        g.ExtraCategoryIds = GroomerProfile.JoinExtraCategoryIds(selected, primary);
        g.Address = Address.Trim();
        g.City = City.Trim();
        g.Latitude = Latitude;
        g.Longitude = Longitude;
        g.About = About.Trim();
        if (!PhoneValidator.TryNormalize(Phone, out var phoneNorm, required: true))
        {
            Message = _L["Phone_Invalid"].Value;
            await FillAsync();
            return Page();
        }
        g.Phone = phoneNorm;
        g.ImageUrl = ImageUrl;
        g.PriceUnit = PriceUnit;
        g.StartingPrice = StartingPrice;
        g.AcceptedSpecies = string.IsNullOrWhiteSpace(AcceptedSpecies) ? PetSpecies.DefaultAcceptedList : AcceptedSpecies;
        g.AcceptsSeniorPets = AcceptsSeniorPets;
        g.AcceptsAnxiousPets = AcceptsAnxiousPets;
        g.IsFeatured = IsFeatured;
        g.IsActive = IsActive;
        await Db.SaveChangesAsync();

        if (IsHotelFromCategories(selected))
        {
            await HotelCoreExtras.SyncAsync(Db, g.Id, ExtraBathPrice, ExtraMedsPrice);
            await HotelPrivateCameraExtra.SyncAsync(Db, g.Id, OffersPrivateCamera, PrivateCameraPrice);
        }
        else
            await HotelPrivateCameraExtra.SyncAsync(Db, g.Id, enabled: false);

        Message = _L["BizPanel_Updated"].Value;
        await FillAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAddAmenityAsync()
    {
        if (await LoadGroomerAsync() is IActionResult r) return r;
        if (!string.IsNullOrWhiteSpace(NewAmenityLabel))
        {
            Db.Amenities.Add(new BusinessAmenity
            {
                GroomerId = Profile!.Id,
                Label = NewAmenityLabel.Trim(),
                Icon = string.IsNullOrWhiteSpace(NewAmenityIcon) ? "✓" : NewAmenityIcon,
                SortOrder = await Db.Amenities.CountAsync(a => a.GroomerId == Profile.Id)
            });
            await Db.SaveChangesAsync();
        }
        return RedirectToPage((string?)null, (string?)null, "biz-amenities");
    }

    public async Task<IActionResult> OnPostAddExtraAsync()
    {
        if (await LoadGroomerAsync() is IActionResult r) return r;
        if (!string.IsNullOrWhiteSpace(NewExtraName))
        {
            var name = NewExtraName.Trim();
            if (HotelCoreExtras.IsManagedHotelExtra(name))
            {
                Message = _L["BizPanel_HotelExtrasHint"].Value;
                await FillAsync();
                return RedirectToPage((string?)null, (string?)null, "biz-hotel-extras");
            }

            Db.ServiceExtras.Add(new ServiceExtra
            {
                GroomerId = Profile!.Id,
                Name = name,
                Price = NewExtraPrice < 0 ? 0 : NewExtraPrice,
                IsActive = true
            });
            await Db.SaveChangesAsync();
        }
        return RedirectToPage((string?)null, (string?)null, "biz-extras");
    }

    public async Task<IActionResult> OnPostAddServiceAsync()
    {
        if (await LoadGroomerAsync() is IActionResult r) return r;
        if (!string.IsNullOrWhiteSpace(NewServiceName) && NewServicePrice > 0)
        {
            var p = NewServicePrice;
            Db.Services.Add(new GroomerService
            {
                GroomerId = Profile!.Id,
                Name = NewServiceName.Trim(),
                Description = NewServiceName.Trim(),
                BillingUnit = NewServiceUnit,
                PriceSmall = p,
                PriceMedium = p + 10,
                PriceLarge = p + 20,
                PriceGiant = p + 30,
                DurationMinutes = NewServiceUnit == "noche" ? 1440 : 60
            });
            await Db.SaveChangesAsync();
        }
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
            var cam = await HotelPrivateCameraExtra.GetAsync(Db, g.Id);
            OffersPrivateCamera = cam.Enabled;
            PrivateCameraPrice = cam.Price;
            var core = await HotelCoreExtras.GetPricesAsync(Db, g.Id);
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
