using Caygnus.ResumableConversation.Api.Enums;

namespace Caygnus.ResumableConversation.Api.DTOs;

public class RunResponse
{
    public Guid RunId { get; set; }

    public Guid ConversationId { get; set; }

    public Guid UserMessageId { get; set; }

    public RunStatus Status { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? FailureReason { get; set; }

    public List<RunEventResponse> Events { get; set; } = new();
}