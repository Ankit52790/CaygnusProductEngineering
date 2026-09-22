using Caygnus.ResumableConversation.Api.Data;
using Caygnus.ResumableConversation.Api.Entities;
using Caygnus.ResumableConversation.Api.Enums;
using Microsoft.EntityFrameworkCore;

namespace Caygnus.ResumableConversation.Api.Services;

public class RunProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IFakeResponseGenerator _generator;
    private readonly EventStreamBroker _eventStreamBroker;

    public RunProcessor(
        IServiceScopeFactory scopeFactory,
        IFakeResponseGenerator generator,
        EventStreamBroker eventStreamBroker)
    {
        _scopeFactory = scopeFactory;
        _generator = generator;
        _eventStreamBroker = eventStreamBroker;
    }

    public async Task ProcessAsync(Guid runId)
    {
        using var scope = _scopeFactory.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var run = await dbContext.Runs
            .Include(x => x.UserMessage)
            .FirstOrDefaultAsync(x => x.Id == runId);

        if (run == null)
        {
            return;
        }

        // Don't process an already-terminal run again.
        if (run.Status is RunStatus.Completed or RunStatus.Failed)
        {
            return;
        }

        try
        {
            // Get all persisted events for this run.
            var existingEvents = await dbContext.RunEvents
                .Where(x => x.RunId == run.Id)
                .OrderBy(x => x.Sequence)
                .ToListAsync();

            // If this is the first attempt, create run.started.
            if (existingEvents.Count == 0)
            {
                await SaveEventAsync(
                    dbContext,
                    run.Id,
                    1,
                    "run.started",
                    null);

                existingEvents = await dbContext.RunEvents
                    .Where(x => x.RunId == run.Id)
                    .OrderBy(x => x.Sequence)
                    .ToListAsync();
            }

            run.Status = RunStatus.Running;

            await dbContext.SaveChangesAsync();

            // Generate the deterministic response.
            var chunks = _generator.Generate(run.UserMessage.Content);

            // Number of tokens already persisted.
            var persistedTokenCount = existingEvents
                .Count(x => x.EventType == "token");

            // Continue from the first token that has not been persisted.
            for (var i = persistedTokenCount; i < chunks.Count; i++)
            {
                var nextSequence = await GetNextSequenceAsync(
                    dbContext,
                    run.Id);

                await SaveEventAsync(
                    dbContext,
                    run.Id,
                    nextSequence,
                    "token",
                    chunks[i]);

                await Task.Delay(100);
            }

            // Check whether completion was already persisted.
            var completionExists = await dbContext.RunEvents
                .AnyAsync(x =>
                    x.RunId == run.Id &&
                    x.EventType == "run.completed");

            if (!completionExists)
            {
                var nextSequence = await GetNextSequenceAsync(
                    dbContext,
                    run.Id);

                await SaveEventAsync(
                    dbContext,
                    run.Id,
                    nextSequence,
                    "run.completed",
                    null);
            }

            run.Status = RunStatus.Completed;
            run.CompletedAt = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            run.Status = RunStatus.Failed;
            run.FailureReason = ex.Message;

            var failureExists = await dbContext.RunEvents
                .AnyAsync(x =>
                    x.RunId == run.Id &&
                    x.EventType == "run.failed");

            if (!failureExists)
            {
                var nextSequence = await GetNextSequenceAsync(
                    dbContext,
                    run.Id);

                await SaveEventAsync(
                    dbContext,
                    run.Id,
                    nextSequence,
                    "run.failed",
                    ex.Message);
            }

            await dbContext.SaveChangesAsync();
        }
    }

    private async Task SaveEventAsync(
        AppDbContext dbContext,
        Guid runId,
        long sequence,
        string eventType,
        string? text)
    {
        var runEvent = new RunEvent
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Sequence = sequence,
            EventType = eventType,
            Text = text,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.RunEvents.Add(runEvent);

        // Persist before publishing.
        await dbContext.SaveChangesAsync();

        // Notify connected SSE clients.
        _eventStreamBroker.Publish(runEvent);
    }

    private static async Task<long> GetNextSequenceAsync(
        AppDbContext dbContext,
        Guid runId)
    {
        var lastSequence = await dbContext.RunEvents
            .Where(x => x.RunId == runId)
            .Select(x => (long?)x.Sequence)
            .MaxAsync() ?? 0;

        return lastSequence + 1;
    }
}