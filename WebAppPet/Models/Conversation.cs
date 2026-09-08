using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

public class Conversation
{
    public int Id { get; set; }

    public int ClientId { get; set; }
    public AppUser Client { get; set; } = null!;

    public int GroomerId { get; set; }
    public GroomerProfile Groomer { get; set; } = null!;

    public int? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

public class ChatMessage
{
    public int Id { get; set; }

    public int ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    public int SenderUserId { get; set; }
    public AppUser Sender { get; set; } = null!;

    [Required, MaxLength(2000)]
    public string Body { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}
