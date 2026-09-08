using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Chat;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public IndexModel(AppDbContext db, AuthService auth)
    {
        _db = db;
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

    public bool UseHistoryBack { get; private set; }

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
        if (!await LoadAsync(userId) || Conversation == null || Groomer == null)
            return RedirectToPage("/Chat/Inbox");

        var text = (Body ?? "").Trim();
        if (string.IsNullOrEmpty(text))
        {
            ErrorMessage = CatalogLocalizer.Loc("Escribe un mensaje.", "Write a message.");
            return Page();
        }

        if (text.Length > 2000) text = text[..2000];

        _db.ChatMessages.Add(new ChatMessage
        {
            ConversationId = Conversation.Id,
            SenderUserId = userId,
            Body = text,
            SentAt = DateTime.UtcNow
        });
        Conversation.LastMessageAt = DateTime.UtcNow;

        var recipientId = userId == Conversation.ClientId ? Groomer.UserId : Conversation.ClientId;
        _db.Notifications.Add(new AppNotification
        {
            UserId = recipientId,
            Title = CatalogLocalizer.Loc("Nuevo mensaje", "New message"),
            Message = text.Length > 80 ? text[..80] + "…" : text,
            Type = "chat"
        });

        await _db.SaveChangesAsync();

        // Stick a concrete returnUrl so post-redirect does not treat Chat as Referer.
        var stickyReturn = SafeLocalUrl(ReturnUrl)
            ?? (UseHistoryBack ? BackHref : null)
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
        UseHistoryBack = false;

        var localReturn = SafeLocalUrl(ReturnUrl);
        if (localReturn != null)
        {
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
                // Same-host previous page (e.g. Details, Appointments) — use history when possible.
                BackHref = refererPath;
                UseHistoryBack = true;
                return;
            }
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
        Groomer = await _db.Groomers.Include(g => g.Category).FirstOrDefaultAsync(g => g.Id == GroomerId);
        if (Groomer == null) return false;

        var isProvider = Groomer.UserId == userId;

        if (ConversationId.HasValue)
        {
            Conversation = await _db.Conversations.FirstOrDefaultAsync(c =>
                c.Id == ConversationId.Value &&
                c.GroomerId == GroomerId &&
                (c.ClientId == userId || isProvider));
        }
        else if (!isProvider)
        {
            // Clients (and groomers messaging another business) can start or resume a thread.
            Conversation = await _db.Conversations
                .FirstOrDefaultAsync(c => c.GroomerId == GroomerId && c.ClientId == userId);

            if (Conversation == null)
            {
                Conversation = new Conversation
                {
                    ClientId = userId,
                    GroomerId = GroomerId,
                    AppointmentId = AppointmentId,
                    CreatedAt = DateTime.UtcNow,
                    LastMessageAt = DateTime.UtcNow
                };
                _db.Conversations.Add(Conversation);
                await _db.SaveChangesAsync();
            }
            else if (AppointmentId.HasValue && Conversation.AppointmentId == null)
            {
                Conversation.AppointmentId = AppointmentId;
                await _db.SaveChangesAsync();
            }
        }
        else
        {
            // Provider must open a specific conversation (from inbox).
            return false;
        }

        if (Conversation == null) return false;

        ConversationId = Conversation.Id;

        Messages = await _db.ChatMessages
            .Include(m => m.Sender)
            .Where(m => m.ConversationId == Conversation.Id)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        var unread = Messages.Where(m => m.SenderUserId != userId && !m.IsRead).ToList();
        if (unread.Count > 0)
        {
            foreach (var m in unread) m.IsRead = true;
            await _db.SaveChangesAsync();
        }

        return true;
    }
}
