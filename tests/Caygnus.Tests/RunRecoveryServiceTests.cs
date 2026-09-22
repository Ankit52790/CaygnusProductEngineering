using Caygnus.ResumableConversation.Api.Data;
using Caygnus.ResumableConversation.Api.Entities;
using Caygnus.ResumableConversation.Api.Enums;
using Caygnus.ResumableConversation.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Caygnus.Tests;

public class RunRecoveryServiceTests
{
    [Fact]
    public async Task StartAsync_RecoversRunningRun_AndCompletesIt()
    {
        await using var provider = TestInfrastructure.CreateProvider();

        Guid runId;

        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            (_, runId) =
                await TestInfrastructure.SeedRunAsync(
                    db,
                    RunStatus.Running);

            await TestInfrastructure.AddEventAsync(
                db,
                runId,
                1,
                "run.started");

            await TestInfrastructure.AddEventAsync(
                db,
                runId,
                2,
                "token",
                "Hello");

            await TestInfrastructure.AddEventAsync(
                db,
                runId,
                3,
                "token",
                "!");

            // Verify that the Running status was actually persisted.
            var seededRun = await db.Runs
                .AsNoTracking()
                .SingleAsync(x => x.Id == runId);

            Assert.Equal(
                RunStatus.Running,
                seededRun.Status);
        }

        using var scope = provider.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var broker = scope.ServiceProvider
            .GetRequiredService<EventStreamBroker>();

        var recovery = scope.ServiceProvider
            .GetRequiredService<RunRecoveryService>();

        // Load the persisted run using the fresh DbContext.
        var run = dbContext.Runs
            .Single(x => x.Id == runId);

        Assert.Equal(
            RunStatus.Running,
            run.Status);

        var runIdToRecover = run.Id;

        var subscriberId = broker.Subscribe(
            runIdToRecover,
            out var reader);

        await recovery.StartAsync(
            CancellationToken.None);

        RunEvent? completedEvent = null;

        while (await reader.WaitToReadAsync())
        {
            while (reader.TryRead(out var runEvent))
            {
                if (runEvent.EventType == "run.completed")
                {
                    completedEvent = runEvent;
                    break;
                }
            }

            if (completedEvent != null)
            {
                break;
            }
        }

        broker.Unsubscribe(
            runIdToRecover,
            subscriberId);

        Assert.NotNull(completedEvent);

        // The recovery processor uses its own DbContext,
        // so verify using another fresh context.
        using var verificationScope = provider.CreateScope();

        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var recoveredEvents = await verificationDb.RunEvents
            .Where(x => x.RunId == runIdToRecover)
            .OrderBy(x => x.Sequence)
            .ToListAsync();

        Assert.Equal(
            42,
            recoveredEvents.Count);

        Assert.Equal(
            Enumerable.Range(1, 42).Select(x => (long)x),
            recoveredEvents.Select(x => x.Sequence));

        var refreshedRun = await verificationDb.Runs
            .SingleAsync(x => x.Id == runIdToRecover);

        Assert.Equal(
            RunStatus.Completed,
            refreshedRun.Status);
    }
}