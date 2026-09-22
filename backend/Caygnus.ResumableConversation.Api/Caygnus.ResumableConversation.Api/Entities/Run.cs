using Caygnus.ResumableConversation.Api.Enums;

namespace Caygnus.ResumableConversation.Api.Entities;

public class Run
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public Guid UserMessageId { get; set; }

    public RunStatus Status { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? FailureReason { get; set; }

    public Conversation Conversation { get; set; } = null!;

    public Message UserMessage { get; set; } = null!;

    public ICollection<RunEvent> Events { get; set; } = new List<RunEvent>();
}