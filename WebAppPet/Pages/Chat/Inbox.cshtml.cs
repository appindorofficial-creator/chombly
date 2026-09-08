using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Chat;

public class InboxModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public InboxModel(AppDbContext db, AuthService auth)
    {
        _db = db;
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

        var groomer = await _db.Groomers.AsNoTracking().FirstOrDefaultAsync(g => g.UserId == userId);
        MyGroomerId = groomer?.Id;

        Items = await _db.Conversations
            .Include(c => c.Groomer)
            .Include(c => c.Client)
            .Where(c => c.ClientId == userId || (groomer != null && c.GroomerId == groomer.Id))
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync();

        if (Items.Count > 0)
        {
            var ids = Items.Select(c => c.Id).ToList();
            var messages = await _db.ChatMessages
                .AsNoTracking()
                .Where(m => ids.Contains(m.ConversationId))
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            foreach (var m in messages)
            {
                if (LastPreview.ContainsKey(m.ConversationId)) continue;
                LastPreview[m.ConversationId] = m.Body.Length > 80 ? m.Body[..80] + "…" : m.Body;
            }
        }

        return Page();
    }
}
