using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Pets;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly IStringLocalizer<SharedResource> _L;

    public IndexModel(AppDbContext db, AuthService auth, IStringLocalizer<SharedResource> L)
    {
        _db = db;
        _auth = auth;
        _L = L;
    }

    public bool IsGuest { get; set; }
    public List<Pet> Pets { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
        {
            IsGuest = true;
            return Page();
        }

        Pets = await _db.Pets.Where(p => p.OwnerId == userId).OrderBy(p => p.Name).ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = "/Pets" });

        var pet = await _db.Pets.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == userId);
        if (pet != null)
        {
            var hasAppts = await _db.Appointments.AnyAsync(a => a.PetId == id);
            if (!hasAppts)
            {
                _db.Pets.Remove(pet);
                await _db.SaveChangesAsync();
            }
        }

        return RedirectToPage();
    }

    public string SizeLabel(PetSize s) => s switch
    {
        PetSize.Small => _L["Size_Small"].Value,
        PetSize.Large => _L["Size_Large"].Value,
        PetSize.Giant => _L["Size_Giant"].Value,
        _ => _L["Size_Medium"].Value
    };
}
