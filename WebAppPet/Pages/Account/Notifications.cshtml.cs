using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Account;

public class NotificationsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public NotificationsModel(AppDbContext db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    public List<AppNotification> Items { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        Items = await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        foreach (var n in Items.Where(x => !x.IsRead))
            n.IsRead = true;
        await _db.SaveChangesAsync();

        return Page();
    }
}
