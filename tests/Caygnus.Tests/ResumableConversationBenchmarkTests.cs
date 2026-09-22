using Caygnus.ResumableConversation.Api.Data;
using Caygnus.ResumableConversation.Api.Entities;
using Caygnus.ResumableConversation.Api.Enums;
using Caygnus.ResumableConversation.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Caygnus.Tests;

public class ResumableConversationBenchmarkTests
{
    [Fact]
    public async Task Benchmark_RunProduces30PlusOrderedEventsWithNoGapsOrDuplicates()
    {
        var databaseName = Guid.NewGuid().ToString();

        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>(
            options => options.UseInMemoryDatabase(databaseName));

        services.AddSingleton<EventStreamBroker>();
        services.AddSingleton<IFakeResponseGenerator, FakeResponseGenerator>();
        services.AddScoped<RunProcessor>();

        var provider = services.BuildServiceProvider();

        Guid runId;

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var conversationId = Guid.NewGuid();
            var messageId = Guid.NewGuid();

            runId = Guid.NewGuid();

            db.Conversations.Add(
                new Conversation
                {
                    Id = conversationId,
                    CreatedAt = DateTimeOffset.UtcNow
                });

            db.Messages.Add(
                new Message
                {
                    Id = messageId,
                    ConversationId = conversationId,
                    Role = MessageRole.User,
                    Content = "benchmark",
                    CreatedAt = DateTimeOffset.UtcNow
                });

            db.Runs.Add(
                new Run
                {
                    Id = runId,
                    ConversationId = conversationId,
                    UserMessageId = messageId,
                    Status = RunStatus.Running,
                    StartedAt = DateTimeOffset.UtcNow
                });

            await db.SaveChangesAsync();
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var processor = scope.ServiceProvider
                .GetRequiredService<RunProcessor>();

            await processor.ProcessAsync(runId);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var run = await db.Runs
                .SingleAsync(x => x.Id == runId);

            var events = await db.RunEvents
                .Where(x => x.RunId == runId)
                .OrderBy(x => x.Sequence)
                .ToListAsync();

            Assert.True(
                events.Count >= 30,
                $"Expected at least 30 events but found {events.Count}.");

            Assert.Equal(
                RunStatus.Completed,
                run.Status);

            var sequences = events
                .Select(x => x.Sequence)
                .ToArray();

            Assert.Equal(
                sequences.Length,
                sequences.Distinct().Count());

            var expectedSequences = Enumerable
                .Range(1, events.Count)
                .Select(x => (long)x)
                .ToArray();

            Assert.Equal(
                expectedSequences,
                sequences);

            Assert.Equal(
                "run.started",
                events.First().EventType);

            Assert.Equal(
                "run.completed",
                events.Last().EventType);

            Assert.All(
                events.Skip(1).SkipLast(1),
                x => Assert.Equal("token", x.EventType));

            var reconstructedResponse = string.Concat(
                events
                    .Where(x => x.EventType == "token")
                    .Select(x => x.Text));

            Assert.False(
                string.IsNullOrWhiteSpace(reconstructedResponse));

            Console.WriteLine(
                $"Benchmark observed event count: {events.Count}");

            Console.WriteLine(
                $"Benchmark final state: {run.Status}");

            Console.WriteLine(
                $"Benchmark reconstructed response: {reconstructedResponse}");
        }
    }
}