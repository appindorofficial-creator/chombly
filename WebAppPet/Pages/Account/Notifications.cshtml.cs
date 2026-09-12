using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;
using WebAppPet.Ui;

namespace WebAppPet.Pages.Account;

public class NotificationsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly IStringLocalizer<SharedResource> _L;

    public NotificationsModel(AppDbContext db, AuthService auth, IStringLocalizer<SharedResource> L)
    {
        _db = db;
        _auth = auth;
        _L = L;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string BackHref { get; private set; } = "/Account/Profile";

    public List<AppNotification> Items { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = BuildLoginReturn() });

        BackHref = ResolveBackHref();

        Items = await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        // Mark read in DB without mutating the in-memory list, so this visit still shows unread styling.
        if (Items.Any(x => !x.IsRead))
        {
            await _db.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = BuildLoginReturn() });

        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (n != null)
        {
            await ClearDeliveryLinksAsync(new[] { n.Id });
            _db.Notifications.Remove(n);
            await _db.SaveChangesAsync();
            AppFlash.Toast(this, "✓ " + _L["Feedback_Deleted"].Value);
        }

        return RedirectToPage(new { returnUrl = ReturnUrl });
    }

    public async Task<IActionResult> OnPostClearAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = BuildLoginReturn() });

        var items = await _db.Notifications.Where(n => n.UserId == userId).ToListAsync();
        if (items.Count > 0)
        {
            await ClearDeliveryLinksAsync(items.Select(x => x.Id));
            _db.Notifications.RemoveRange(items);
            await _db.SaveChangesAsync();
            AppFlash.Toast(this, "✓ " + _L["Feedback_DeletedAll"].Value);
        }

        return RedirectToPage(new { returnUrl = ReturnUrl });
    }

    private async Task ClearDeliveryLinksAsync(IEnumerable<int> notificationIds)
    {
        var ids = notificationIds.ToList();
        if (ids.Count == 0) return;

        var deliveries = await _db.ReminderDeliveries
            .Where(d => d.AppNotificationId != null && ids.Contains(d.AppNotificationId.Value))
            .ToListAsync();
        foreach (var d in deliveries)
            d.AppNotificationId = null;
    }

    private string BuildLoginReturn()
    {
        var path = "/Account/Notifications";
        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            path += "?returnUrl=" + Uri.EscapeDataString(ReturnUrl);
        return path;
    }

    private string ResolveBackHref()
    {
        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            return ReturnUrl!;

        var referer = Request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase))
        {
            var path = uri.PathAndQuery;
            if (!path.StartsWith("/Account/Notifications", StringComparison.OrdinalIgnoreCase) &&
                Url.IsLocalUrl(path))
                return path;
        }

        return Url.Page("/Account/Profile") ?? "/Account/Profile";
    }
}
