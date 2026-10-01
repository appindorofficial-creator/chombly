using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Conversations.OpenConversation;
using WebAppPet.Application.Conversations.SendMessage;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Identity;
using WebAppPet.Localization;

namespace WebAppPet.Pages.Chat;

public class IndexModel : PageModel
{
    private readonly OpenConversationHandler _open;
    private readonly SendMessageHandler _send;
    private readonly AuthService _auth;

    public IndexModel(OpenConversationHandler open, SendMessageHandler send, AuthService auth)
    {
        _open = open;
        _send = send;
        _auth = auth;
    }

    [BindProperty(SupportsGet = true)]
    public int GroomerId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? AppointmentId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? ConversationId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public string Body { get; set; } = string.Empty;

    public Conversation? Conversation { get; set; }
    public GroomerProfile? Groomer { get; set; }
    public List<ChatMessage> Messages { get; set; } = new();
    public int CurrentUserId { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>Resolved href for the header back control.</summary>
    public string BackHref { get; private set; } = "/Chat/Inbox";

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        CurrentUserId = userId;
        ResolveBackNavigation();
        if (!await LoadAsync(userId))
            return RedirectToPage("/Chat/Inbox");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        CurrentUserId = userId;
        ResolveBackNavigation();
        if (!await LoadAsync(userId) || Conversation == null)
            return RedirectToPage("/Chat/Inbox");

        var outcome = await _send.HandleAsync(new SendMessageCommand(userId, Conversation.Id, Body));
        if (outcome == SendMessageOutcome.NotFound)
            return RedirectToPage("/Chat/Inbox");
        if (outcome == SendMessageOutcome.EmptyMessage)
        {
            ErrorMessage = CatalogLocalizer.Loc("Escribe un mensaje.", "Write a message.");
            return Page();
        }

        // Stick a concrete returnUrl so post-redirect does not treat Chat as Referer.
        var stickyReturn = SafeLocalUrl(ReturnUrl)
            ?? SafeLocalUrl(BackHref)
            ?? Url.Page("/Chat/Inbox");

        return RedirectToPage(new
        {
            groomerId = GroomerId,
            appointmentId = AppointmentId,
            conversationId = Conversation.Id,
            returnUrl = stickyReturn
        });
    }

    private void ResolveBackNavigation()
    {
        var localReturn = SafeLocalUrl(ReturnUrl);
        if (localReturn != null && !IsChatIndexPath(localReturn))
        {
            // Prefer an explicit return target (e.g. appointment details) — never history.back(),
            // which traps users in a Details ↔ Chat history stack on mobile.
            BackHref = localReturn;
            return;
        }

        if (TryGetLocalRefererPath(out var refererPath))
        {
            if (IsInboxPath(refererPath))
            {
                BackHref = Url.Page("/Chat/Inbox") ?? "/Chat/Inbox";
                return;
            }

            if (!IsChatIndexPath(refererPath))
            {
                BackHref = refererPath;
                return;
            }
        }

        if (AppointmentId is int apptId && apptId > 0)
        {
            BackHref = Url.Page("/Appointments/Details", new { id = apptId, returnUrl = Url.Page("/Appointments/Index") })
                ?? "/Appointments";
            return;
        }

        if (_auth.CurrentUserId is not null)
        {
            BackHref = Url.Page("/Chat/Inbox") ?? "/Chat/Inbox";
            return;
        }

        BackHref = Url.Page("/Account/Profile") ?? "/Account/Profile";
    }

    private string? SafeLocalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        return Url.IsLocalUrl(url) ? url : null;
    }

    private bool TryGetLocalRefererPath(out string pathAndQuery)
    {
        pathAndQuery = string.Empty;
        var referer = Request.Headers.Referer.ToString();
        if (string.IsNullOrWhiteSpace(referer)) return false;
        if (!Uri.TryCreate(referer, UriKind.Absolute, out var uri)) return false;
        if (!string.Equals(uri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase))
            return false;

        pathAndQuery = uri.PathAndQuery;
        return Url.IsLocalUrl(pathAndQuery);
    }

    private static bool IsInboxPath(string pathAndQuery)
    {
        var path = pathAndQuery.Split('?', 2)[0];
        return path.Equals("/Chat/Inbox", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsChatIndexPath(string pathAndQuery)
    {
        var path = pathAndQuery.Split('?', 2)[0];
        return path.Equals("/Chat/Index", StringComparison.OrdinalIgnoreCase)
               || path.Equals("/Chat", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> LoadAsync(int userId)
    {
        var thread = await _open.HandleAsync(new OpenConversationCommand(userId, GroomerId, ConversationId, AppointmentId));
        if (thread is null) return false;

        Groomer = thread.Groomer;
        Conversation = thread.Conversation;
        Messages = thread.Messages;
        ConversationId = Conversation.Id;
        return true;
    }
}
