using Microsoft.EntityFrameworkCore;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Conversations.SendMessage;

public enum SendMessageOutcome
{
    Sent,
    EmptyMessage,
    /// <summary>The conversation does not exist or the user is neither its client nor its business.</summary>
    NotFound
}

/// <summary>Saves the message and notifies the other side of the conversation.</summary>
public class SendMessageHandler
{
    public const int MaxLength = 2000;
    private const int PreviewLength = 80;

    private readonly AppDbContext _db;

    public SendMessageHandler(AppDbContext db) => _db = db;

    public async Task<SendMessageOutcome> HandleAsync(SendMessageCommand command, CancellationToken ct = default)
    {
        var conversation = await _db.Conversations.Include(c => c.Groomer)
            .FirstOrDefaultAsync(c => c.Id == command.ConversationId, ct);
        if (conversation is null ||
            (conversation.ClientId != command.UserId && conversation.Groomer.UserId != command.UserId))
            return SendMessageOutcome.NotFound;

        var text = (command.Body ?? "").Trim();
        if (text.Length == 0)
            return SendMessageOutcome.EmptyMessage;
        if (text.Length > MaxLength)
            text = text[..MaxLength];

        var now = DateTime.UtcNow;
        _db.ChatMessages.Add(new ChatMessage
        {
            ConversationId = conversation.Id,
            SenderUserId = command.UserId,
            Body = text,
            SentAt = now
        });
        conversation.LastMessageAt = now;

        var recipientId = command.UserId == conversation.ClientId ? conversation.Groomer.UserId : conversation.ClientId;
        _db.Notifications.Add(new AppNotification
        {
            UserId = recipientId,
            Title = "Nuevo mensaje",
            Message = text.Length > PreviewLength ? text[..PreviewLength] + "…" : text,
            Type = "chat"
        });

        await _db.SaveChangesAsync(ct);
        return SendMessageOutcome.Sent;
    }
}
