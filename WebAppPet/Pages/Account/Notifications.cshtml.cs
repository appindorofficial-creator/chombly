using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using WebAppPet.Application.Notifications.ClearNotifications;
using WebAppPet.Application.Notifications.DeleteNotification;
using WebAppPet.Application.Notifications.OpenInbox;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Identity;
using WebAppPet.Localization;
using WebAppPet.Ui;

namespace WebAppPet.Pages.Account;

public class NotificationsModel : PageModel
{
    private readonly AuthService _auth;
    private readonly IStringLocalizer<SharedResource> _L;
    private readonly OpenInboxHandler _openInbox;
    private readonly DeleteNotificationHandler _deleteNotification;
    private readonly ClearNotificationsHandler _clearNotifications;

    public NotificationsModel(
        AuthService auth,
        IStringLocalizer<SharedResource> L,
        OpenInboxHandler openInbox,
        DeleteNotificationHandler deleteNotification,
        ClearNotificationsHandler clearNotifications)
    {
        _auth = auth;
        _L = L;
        _openInbox = openInbox;
        _deleteNotification = deleteNotification;
        _clearNotifications = clearNotifications;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string BackHref { get; private set; } = "/Account/Profile";

    public List<AppNotification> Items { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = BuildLoginReturn() });

        BackHref = ResolveBackHref();
        Items = await _openInbox.HandleAsync(new OpenInboxCommand(userId), ct);
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = BuildLoginReturn() });

        if (await _deleteNotification.HandleAsync(new DeleteNotificationCommand(userId, id), ct))
            AppFlash.Toast(this, "✓ " + _L["Feedback_Deleted"].Value);

        return RedirectToPage(new { returnUrl = ReturnUrl });
    }

    public async Task<IActionResult> OnPostClearAsync(CancellationToken ct)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = BuildLoginReturn() });

        if (await _clearNotifications.HandleAsync(new ClearNotificationsCommand(userId), ct) > 0)
            AppFlash.Toast(this, "✓ " + _L["Feedback_DeletedAll"].Value);

        return RedirectToPage(new { returnUrl = ReturnUrl });
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
