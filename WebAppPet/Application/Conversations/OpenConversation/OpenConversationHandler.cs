using Microsoft.EntityFrameworkCore;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Conversations.OpenConversation;

public sealed record ChatThread(GroomerProfile Groomer, Conversation Conversation, List<ChatMessage> Messages);

/// <summary>
/// The thread with its messages, oldest first. Messages the user received are marked read.
/// Null when the business does not exist or the user may not see the conversation.
/// </summary>
public class OpenConversationHandler
{
    private readonly AppDbContext _db;

    public OpenConversationHandler(AppDbContext db) => _db = db;

    public async Task<ChatThread?> HandleAsync(OpenConversationCommand command, CancellationToken ct = default)
    {
        var groomer = await _db.Groomers.Include(g => g.Category)
            .FirstOrDefaultAsync(g => g.Id == command.GroomerId, ct);
        if (groomer is null)
            return null;

        var isProvider = groomer.UserId == command.UserId;
        Conversation? conversation;

        if (command.ConversationId is int conversationId)
        {
            conversation = await _db.Conversations.FirstOrDefaultAsync(c =>
                c.Id == conversationId &&
                c.GroomerId == groomer.Id &&
                (c.ClientId == command.UserId || isProvider), ct);
        }
        else if (!isProvider)
        {
            // Clients (and businesses writing to another business) start or resume their own thread.
            conversation = await _db.Conversations
                .FirstOrDefaultAsync(c => c.GroomerId == groomer.Id && c.ClientId == command.UserId, ct);

            if (conversation is null)
            {
                conversation = new Conversation
                {
                    ClientId = command.UserId,
                    GroomerId = groomer.Id,
                    AppointmentId = command.AppointmentId,
                    CreatedAt = DateTime.UtcNow,
                    LastMessageAt = DateTime.UtcNow
                };
                _db.Conversations.Add(conversation);
                await _db.SaveChangesAsync(ct);
            }
            else if (command.AppointmentId.HasValue && conversation.AppointmentId is null)
            {
                conversation.AppointmentId = command.AppointmentId;
                await _db.SaveChangesAsync(ct);
            }
        }
        else
        {
            conversation = null;
        }

        if (conversation is null)
            return null;

        var messages = await _db.ChatMessages
            .Include(m => m.Sender)
            .Where(m => m.ConversationId == conversation.Id)
            .OrderBy(m => m.SentAt)
            .ToListAsync(ct);

        var unread = messages.Where(m => m.SenderUserId != command.UserId && !m.IsRead).ToList();
        if (unread.Count > 0)
        {
            foreach (var m in unread) m.IsRead = true;
            await _db.SaveChangesAsync(ct);
        }

        return new ChatThread(groomer, conversation, messages);
    }
}
