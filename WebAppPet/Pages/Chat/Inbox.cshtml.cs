using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Conversations.GetInbox;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Identity;

namespace WebAppPet.Pages.Chat;

public class InboxModel : PageModel
{
    private readonly GetInboxHandler _inbox;
    private readonly AuthService _auth;

    public InboxModel(GetInboxHandler inbox, AuthService auth)
    {
        _inbox = inbox;
        _auth = auth;
    }

    public List<Conversation> Items { get; set; } = new();
    public Dictionary<int, string> LastPreview { get; set; } = new();
    /// <summary>When set, conversations for this groomer show the client name.</summary>
    public int? MyGroomerId { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        var view = await _inbox.HandleAsync(new GetInboxQuery(userId));
        MyGroomerId = view.MyGroomerId;
        Items = view.Items;
        LastPreview = view.LastPreview;
        return Page();
    }
}
