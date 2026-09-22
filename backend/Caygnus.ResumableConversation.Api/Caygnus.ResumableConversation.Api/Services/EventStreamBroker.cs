using System.Collections.Concurrent;
using System.Threading.Channels;
using Caygnus.ResumableConversation.Api.Entities;

namespace Caygnus.ResumableConversation.Api.Services;

public class EventStreamBroker
{
    private readonly ConcurrentDictionary<
        Guid,
        ConcurrentDictionary<Guid, Channel<RunEvent>>> _subscribers = new();

    public Guid Subscribe(
        Guid runId,
        out ChannelReader<RunEvent> reader)
    {
        var subscriberId = Guid.NewGuid();

        var channel = Channel.CreateBounded<RunEvent>(
            new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });

        var runSubscribers =
            _subscribers.GetOrAdd(
                runId,
                _ => new ConcurrentDictionary<Guid, Channel<RunEvent>>());

        runSubscribers[subscriberId] = channel;

        reader = channel.Reader;

        return subscriberId;
    }

    public void Unsubscribe(
        Guid runId,
        Guid subscriberId)
    {
        if (!_subscribers.TryGetValue(runId, out var runSubscribers))
        {
            return;
        }

        if (runSubscribers.TryRemove(
            subscriberId,
            out var channel))
        {
            channel.Writer.TryComplete();
        }

        if (runSubscribers.IsEmpty)
        {
            _subscribers.TryRemove(runId, out _);
        }
    }

    public void Publish(RunEvent runEvent)
    {
        if (!_subscribers.TryGetValue(
            runEvent.RunId,
            out var runSubscribers))
        {
            return;
        }

        foreach (var subscriber in runSubscribers)
        {
            subscriber.Value.Writer.TryWrite(runEvent);
        }
    }
}