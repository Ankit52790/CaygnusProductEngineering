namespace Caygnus.ResumableConversation.Api.DTOs;

public class RunEventResponse
{
    public Guid Id { get; set; }

    public long Sequence { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string? Text { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}