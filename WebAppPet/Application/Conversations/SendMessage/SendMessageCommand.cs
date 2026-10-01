namespace WebAppPet.Application.Conversations.SendMessage;

public sealed record SendMessageCommand(int UserId, int ConversationId, string? Body);
