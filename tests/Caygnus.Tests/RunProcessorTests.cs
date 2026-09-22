using Caygnus.ResumableConversation.Api.Data;
using Caygnus.ResumableConversation.Api.Enums;
using Caygnus.ResumableConversation.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Caygnus.Tests;

public class RunProcessorTests
{
    [Fact]
    public async Task ProcessAsync_CreatesOrderedEvents_AndCompletesRun()
    {
        await using var provider = TestInfrastructure.CreateProvider();

        Guid runId;

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            (_, runId) =
                await TestInfrastructure.SeedRunAsync(db);

            var processor = scope.ServiceProvider
                .GetRequiredService<RunProcessor>();

            await processor.ProcessAsync(runId);
        }

        // Use a fresh DbContext because RunProcessor creates its own scope.
        using var verificationScope = provider.CreateScope();

        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var events = await verificationDb.RunEvents
            .Where(x => x.RunId == runId)
            .OrderBy(x => x.Sequence)
            .ToListAsync();

        var run = await verificationDb.Runs
            .SingleAsync(x => x.Id == runId);

        Assert.Equal(RunStatus.Completed, run.Status);

        Assert.Equal(42, events.Count);

        Assert.Equal(
            Enumerable.Range(1, 42).Select(x => (long)x),
            events.Select(x => x.Sequence));

        Assert.Equal("run.started", events.First().EventType);
        Assert.Equal("run.completed", events.Last().EventType);

        var duplicateSequences = events
            .GroupBy(x => x.Sequence)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.Empty(duplicateSequences);
    }

    [Fact]
    public async Task ProcessAsync_ResumesFromPersistedTokens_WithoutDuplicatingThem()
    {
        await using var provider = TestInfrastructure.CreateProvider();

        Guid runId;

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            (_, runId) =
                await TestInfrastructure.SeedRunAsync(db);

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

            await TestInfrastructure.AddEventAsync(
                db,
                runId,
                4,
                "token",
                " ");

            var processor = scope.ServiceProvider
                .GetRequiredService<RunProcessor>();

            await processor.ProcessAsync(runId);
        }

        // Fresh context so we see the events written by RunProcessor.
        using var verificationScope = provider.CreateScope();

        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var events = await verificationDb.RunEvents
            .Where(x => x.RunId == runId)
            .OrderBy(x => x.Sequence)
            .ToListAsync();

        Assert.Equal(42, events.Count);

        Assert.Equal(
            Enumerable.Range(1, 42).Select(x => (long)x),
            events.Select(x => x.Sequence));

        Assert.Equal(
            1,
            events.Count(x =>
                x.Sequence == 2 &&
                x.Text == "Hello"));

        Assert.Equal(
            1,
            events.Count(x =>
                x.Sequence == 3 &&
                x.Text == "!"));

        Assert.Equal(
            1,
            events.Count(x =>
                x.Sequence == 4 &&
                x.Text == " "));
    }

    [Fact]
    public async Task ProcessAsync_WhenGeneratorFailsAfterPersistedOutput_CreatesFailureEvent()
    {
        var generator = new FailingGenerator();

        await using var provider =
            TestInfrastructure.CreateProvider(generator);

        Guid runId;

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            (_, runId) =
                await TestInfrastructure.SeedRunAsync(db);

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
                "partial");

            var processor = scope.ServiceProvider
                .GetRequiredService<RunProcessor>();

            await processor.ProcessAsync(runId);
        }

        // Fresh context because RunProcessor used another DbContext.
        using var verificationScope = provider.CreateScope();

        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var run = await verificationDb.Runs
            .SingleAsync(x => x.Id == runId);

        Assert.Equal(RunStatus.Failed, run.Status);

        Assert.Equal(
            "generator failed",
            run.FailureReason);

        var events = await verificationDb.RunEvents
            .Where(x => x.RunId == runId)
            .OrderBy(x => x.Sequence)
            .ToListAsync();

        Assert.Equal(
            new long[] { 1, 2, 3 },
            events.Select(x => x.Sequence));

        Assert.Equal(
            "run.failed",
            events.Last().EventType);

        Assert.Equal(
            "generator failed",
            events.Last().Text);
    }

    private sealed class FailingGenerator : IFakeResponseGenerator
    {
        public IReadOnlyList<string> Generate(string userMessage)
        {
            throw new InvalidOperationException("generator failed");
        }
    }
}