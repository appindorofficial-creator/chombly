using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Account;

public class ProfileModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public ProfileModel(AppDbContext db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    public AppUser? UserEntity { get; set; }

    public async Task OnGetAsync()
    {
        if (_auth.CurrentUserId is int id)
            UserEntity = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
    }
}
