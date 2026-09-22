namespace Caygnus.ResumableConversation.Api.Services;

public class FakeResponseGenerator : IFakeResponseGenerator
{
    public IReadOnlyList<string> Generate(string userMessage)
    {
        return new[]
        {
            "Hello",
            "!",
            " ",
            "ASP.NET",
            " Core",
            " is",
            " a",
            " cross-platform",
            " framework",
            " for",
            " building",
            " modern",
            " web",
            " applications",
            ".",
            " It",
            " provides",
            " built-in",
            " support",
            " for",
            " dependency",
            " injection",
            ",",
            " configuration",
            ",",
            " logging",
            ",",
            " middleware",
            ",",
            " routing",
            ",",
            " authentication",
            ",",
            " authorization",
            ",",
            " and",
            " high-performance",
            " HTTP",
            " APIs",
            "."
        };
    }
}