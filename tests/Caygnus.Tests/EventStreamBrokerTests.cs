using Caygnus.ResumableConversation.Api.Entities;
using Caygnus.ResumableConversation.Api.Services;

namespace Caygnus.Tests;

public class EventStreamBrokerTests
{
    [Fact]
    public async Task PublishedEvents_AreReceivedInOrder()
    {
        var broker = new EventStreamBroker();

        var runId = Guid.NewGuid();

        var subscriberId = broker.Subscribe(
            runId,
            out var reader);

        var events = Enumerable.Range(1, 5)
            .Select(sequence => new RunEvent
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                Sequence = sequence,
                EventType = "token",
                Text = $"token-{sequence}",
                CreatedAt = DateTimeOffset.UtcNow
            })
            .ToArray();

        foreach (var runEvent in events)
        {
            broker.Publish(runEvent);
        }

        var received = new List<RunEvent>();

        while (received.Count < events.Length &&
               await reader.WaitToReadAsync())
        {
            while (reader.TryRead(out var item))
            {
                received.Add(item);
            }
        }

        broker.Unsubscribe(runId, subscriberId);

        Assert.Equal(
            new long[] { 1, 2, 3, 4, 5 },
            received.Select(x => x.Sequence));
    }

    [Fact]
    public async Task Unsubscribe_StopsReceivingEvents()
    {
        var broker = new EventStreamBroker();

        var runId = Guid.NewGuid();

        var subscriberId = broker.Subscribe(
            runId,
            out var reader);

        broker.Unsubscribe(runId, subscriberId);

        broker.Publish(new RunEvent
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Sequence = 1,
            EventType = "token",
            Text = "ignored",
            CreatedAt = DateTimeOffset.UtcNow
        });

        Assert.False(await reader.WaitToReadAsync());
    }
}