namespace WebAppPet.Application.Conversations.OpenConversation;

/// <summary>
/// Opens a chat with a business. Without <see cref="ConversationId"/> a client starts or resumes their
/// thread with the business; the business itself must name the conversation it wants to open.
/// </summary>
public sealed record OpenConversationCommand(int UserId, int GroomerId, int? ConversationId, int? AppointmentId);
