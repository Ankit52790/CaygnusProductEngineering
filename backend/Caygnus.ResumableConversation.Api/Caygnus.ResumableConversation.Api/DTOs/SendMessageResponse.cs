namespace Caygnus.ResumableConversation.Api.DTOs;

public class SendMessageResponse
{
    public Guid ConversationId { get; set; }

    public Guid MessageId { get; set; }

    public Guid RunId { get; set; }
}