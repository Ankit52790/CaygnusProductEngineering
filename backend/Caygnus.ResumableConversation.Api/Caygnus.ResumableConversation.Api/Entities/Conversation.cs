using System.Text;

namespace Caygnus.ResumableConversation.Api.Entities;

public class Conversation
{
    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Message> Messages { get; set; } = new List<Message>();

    public ICollection<Run> Runs { get; set; } = new List<Run>();
}