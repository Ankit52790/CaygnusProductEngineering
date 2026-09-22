namespace Caygnus.ResumableConversation.Api.DTOs;

public class CursorErrorResponse
{
    public string Error { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public Guid RunId { get; set; }

    public long RequestedCursor { get; set; }

    public long EarliestAvailableSequence { get; set; }

    public long LatestAvailableSequence { get; set; }
}