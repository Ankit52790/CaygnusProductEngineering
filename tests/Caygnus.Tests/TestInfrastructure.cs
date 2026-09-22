using Caygnus.ResumableConversation.Api.Data;
using Caygnus.ResumableConversation.Api.Entities;
using Caygnus.ResumableConversation.Api.Enums;
using Caygnus.ResumableConversation.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Caygnus.Tests;

internal static class TestInfrastructure
{
    public static ServiceProvider CreateProvider(
        IFakeResponseGenerator? generator = null)
    {
        var services = new ServiceCollection();

        var databaseName = $"CaygnusTests-{Guid.NewGuid()}";

        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        services.AddSingleton(
            generator ?? new FakeResponseGenerator());

        services.AddSingleton<EventStreamBroker>();

        services.AddScoped<RunProcessor>();

        services.AddScoped<RunRecoveryService>();

        return services.BuildServiceProvider();
    }

    public static async Task<(Guid ConversationId, Guid RunId)> SeedRunAsync(
        AppDbContext db,
        RunStatus status = RunStatus.Running)
    {
        var conversationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        var conversation = new Conversation
        {
            Id = conversationId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var message = new Message
        {
            Id = messageId,
            ConversationId = conversationId,
            Role = MessageRole.User,
            Content = "Test message",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var run = new Run
        {
            Id = runId,
            ConversationId = conversationId,
            UserMessageId = messageId,
            Status = status,
            StartedAt = DateTimeOffset.UtcNow,
            Conversation = conversation,
            UserMessage = message
        };

        db.Conversations.Add(conversation);
        db.Messages.Add(message);
        db.Runs.Add(run);

        await db.SaveChangesAsync();

        return (conversationId, runId);
    }

    public static async Task AddEventAsync(
        AppDbContext db,
        Guid runId,
        long sequence,
        string eventType,
        string? text = null)
    {
        db.RunEvents.Add(new RunEvent
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Sequence = sequence,
            EventType = eventType,
            Text = text,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
    }
}
