using Microsoft.EntityFrameworkCore;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Conversations.GetInbox;

/// <param name="MyGroomerId">The user's own business, if any: its conversations are titled with the client's name.</param>
/// <param name="LastPreview">The latest message of each conversation, cut to 80 characters, by conversation id.</param>
public sealed record InboxView(int? MyGroomerId, List<Conversation> Items, Dictionary<int, string> LastPreview);

/// <summary>
/// Conversations where the user is the client or the business, most recent activity first.
/// </summary>
public class GetInboxHandler
{
    private const int PreviewLength = 80;

    private readonly AppDbContext _db;

    public GetInboxHandler(AppDbContext db) => _db = db;

    public async Task<InboxView> HandleAsync(GetInboxQuery query, CancellationToken ct = default)
    {
        var myGroomerId = await _db.Groomers.AsNoTracking()
            .Where(g => g.UserId == query.UserId)
            .Select(g => (int?)g.Id)
            .FirstOrDefaultAsync(ct);

        var items = await _db.Conversations.AsNoTracking()
            .Include(c => c.Groomer)
            .Include(c => c.Client)
            .Where(c => c.ClientId == query.UserId || (myGroomerId != null && c.GroomerId == myGroomerId))
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync(ct);

        var preview = new Dictionary<int, string>();
        if (items.Count > 0)
        {
            var ids = items.Select(c => c.Id).ToList();
            var messages = await _db.ChatMessages.AsNoTracking()
                .Where(m => ids.Contains(m.ConversationId))
                .OrderByDescending(m => m.SentAt)
                .Select(m => new { m.ConversationId, m.Body })
                .ToListAsync(ct);

            foreach (var m in messages)
            {
                if (preview.ContainsKey(m.ConversationId)) continue;
                preview[m.ConversationId] = m.Body.Length > PreviewLength ? m.Body[..PreviewLength] + "…" : m.Body;
            }
        }

        return new InboxView(myGroomerId, items, preview);
    }
}
