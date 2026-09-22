using Caygnus.ResumableConversation.Api.Enums;
using System.Text;

namespace Caygnus.ResumableConversation.Api.Entities;

public class Message
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public MessageRole Role { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public Conversation Conversation { get; set; } = null!;

    public Run? Run { get; set; }
}