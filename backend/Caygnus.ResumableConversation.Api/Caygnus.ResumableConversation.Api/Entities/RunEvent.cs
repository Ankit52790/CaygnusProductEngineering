namespace Caygnus.ResumableConversation.Api.Entities;

public class RunEvent
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public long Sequence { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string? Text { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Run Run { get; set; } = null!;
}