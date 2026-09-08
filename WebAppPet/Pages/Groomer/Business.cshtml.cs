using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Groomer;

public class BusinessModel : GroomerPageModel
{
    public BusinessModel(AppDbContext db, AuthService auth) : base(db, auth) { }

    public List<ServiceCategory> Categories { get; set; } = new();
    public List<BusinessAmenity> Amenities { get; set; } = new();
    public List<ServiceExtra> Extras { get; set; } = new();
    public List<GroomerService> Services { get; set; } = new();
    public string? Message { get; set; }

    [BindProperty] public string BusinessName { get; set; } = "";
    [BindProperty] public int? CategoryId { get; set; }
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
        g.BusinessName = BusinessName.Trim();
        g.CategoryId = CategoryId;
        g.Address = Address.Trim();
        g.City = City.Trim();
        g.Latitude = Latitude;
        g.Longitude = Longitude;
        g.About = About.Trim();
        if (!PhoneValidator.TryNormalize(Phone, out var phoneNorm, required: true))
        {
            Message = "Teléfono inválido. Solo números (mín. 7 dígitos). Ej: 7045551234";
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
        Message = "Negocio actualizado.";
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
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddExtraAsync()
    {
        if (await LoadGroomerAsync() is IActionResult r) return r;
        if (!string.IsNullOrWhiteSpace(NewExtraName))
        {
            Db.ServiceExtras.Add(new ServiceExtra
            {
                GroomerId = Profile!.Id,
                Name = NewExtraName.Trim(),
                Price = NewExtraPrice,
                IsActive = true
            });
            await Db.SaveChangesAsync();
        }
        return RedirectToPage();
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
        return RedirectToPage();
    }

    private async Task FillAsync()
    {
        Categories = await Db.Categories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync();
        Profile = await Db.Groomers.Include(g => g.Category).Include(g => g.User).FirstAsync(g => g.Id == Profile!.Id);
        var g = Profile;
        BusinessName = g.BusinessName;
        CategoryId = g.CategoryId;
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
    }
}
