namespace Caygnus.ResumableConversation.Api.Services
{
    public interface IFakeResponseGenerator
    {
        IReadOnlyList<string> Generate(string userMessage);

    }
}
