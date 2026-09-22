using System.Text.Json;
using Caygnus.ResumableConversation.Api.Data;
using Caygnus.ResumableConversation.Api.DTOs;
using Caygnus.ResumableConversation.Api.Entities;
using Caygnus.ResumableConversation.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Caygnus.ResumableConversation.Api.Controllers;

[ApiController]
[Route("api/runs/{runId:guid}/stream")]
public class RunStreamController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly EventStreamBroker _eventStreamBroker;

    public RunStreamController(
        AppDbContext dbContext,
        EventStreamBroker eventStreamBroker)
    {
        _dbContext = dbContext;
        _eventStreamBroker = eventStreamBroker;
    }

    [HttpGet]
    public async Task Stream(
        Guid runId,
        [FromQuery] long after = 0,
        CancellationToken cancellationToken = default)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        if (after < 0)
        {
            await WriteJsonErrorAsync(
                StatusCodes.Status400BadRequest,
                new CursorErrorResponse
                {
                    Error = "invalid_cursor",
                    Message = "The cursor must be zero or greater.",
                    RunId = runId,
                    RequestedCursor = after
                },
                cancellationToken);

            return;
        }

        var runExists = await _dbContext.Runs
            .AsNoTracking()
            .AnyAsync(x => x.Id == runId, cancellationToken);

        if (!runExists)
        {
            await WriteJsonErrorAsync(
                StatusCodes.Status404NotFound,
                new CursorErrorResponse
                {
                    Error = "run_not_found",
                    Message = "The requested run does not exist.",
                    RunId = runId,
                    RequestedCursor = after
                },
                cancellationToken);

            return;
        }

        var firstSequence = await _dbContext.RunEvents
            .Where(x => x.RunId == runId)
            .OrderBy(x => x.Sequence)
            .Select(x => (long?)x.Sequence)
            .FirstOrDefaultAsync(cancellationToken);

        var latestSequence = await _dbContext.RunEvents
            .Where(x => x.RunId == runId)
            .OrderByDescending(x => x.Sequence)
            .Select(x => (long?)x.Sequence)
            .FirstOrDefaultAsync(cancellationToken);

        if (firstSequence.HasValue &&
            after > 0 &&
            after < firstSequence.Value - 1)
        {
            await WriteJsonErrorAsync(
                StatusCodes.Status409Conflict,
                new CursorErrorResponse
                {
                    Error = "stale_cursor",
                    Message =
                        "The requested cursor is older than the earliest retained event.",
                    RunId = runId,
                    RequestedCursor = after,
                    EarliestAvailableSequence = firstSequence.Value,
                    LatestAvailableSequence = latestSequence ?? 0
                },
                cancellationToken);

            return;
        }

        var subscriberId = _eventStreamBroker.Subscribe(
            runId,
            out var reader);

        try
        {
            // Subscribe BEFORE replay so events generated during replay
            // cannot be lost.
            var replayEvents = await _dbContext.RunEvents
                .AsNoTracking()
                .Where(x =>
                    x.RunId == runId &&
                    x.Sequence > after)
                .OrderBy(x => x.Sequence)
                .ToListAsync(cancellationToken);

            var lastSentSequence = after;

            foreach (var runEvent in replayEvents)
            {
                if (runEvent.Sequence <= lastSentSequence)
                {
                    continue;
                }

                await WriteEventAsync(
                    runEvent,
                    cancellationToken);

                lastSentSequence = runEvent.Sequence;
            }

            await Response.Body.FlushAsync(cancellationToken);

            // If replay already reached a terminal event, there is no reason
            // to wait for another live event.
            if (replayEvents.Any(IsTerminal))
            {
                return;
            }

            // Continue with live delivery.
            await foreach (var runEvent in reader.ReadAllAsync(
                cancellationToken))
            {
                // Protect against replay/live overlap.
                if (runEvent.Sequence <= lastSentSequence)
                {
                    continue;
                }

                await WriteEventAsync(
                    runEvent,
                    cancellationToken);

                lastSentSequence = runEvent.Sequence;

                await Response.Body.FlushAsync(cancellationToken);

                if (IsTerminal(runEvent))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the client disconnects.
        }
        finally
        {
            _eventStreamBroker.Unsubscribe(
                runId,
                subscriberId);
        }
    }

    private async Task WriteEventAsync(
        RunEvent runEvent,
        CancellationToken cancellationToken)
    {
        var data = new RunEventResponse
        {
            Id = runEvent.Id,
            Sequence = runEvent.Sequence,
            EventType = runEvent.EventType,
            Text = runEvent.Text,
            CreatedAt = runEvent.CreatedAt
        };

        var json = JsonSerializer.Serialize(data);

        await Response.WriteAsync(
            $"id: {runEvent.Sequence}\n",
            cancellationToken);

        await Response.WriteAsync(
            $"event: {runEvent.EventType}\n",
            cancellationToken);

        await Response.WriteAsync(
            $"data: {json}\n\n",
            cancellationToken);
    }

    private static bool IsTerminal(RunEvent runEvent)
    {
        return runEvent.EventType is
            "run.completed" or
            "run.failed";
    }

    private async Task WriteJsonErrorAsync(
        int statusCode,
        CursorErrorResponse error,
        CancellationToken cancellationToken)
    {
        Response.StatusCode = statusCode;
        Response.ContentType = "application/json";

        await Response.WriteAsJsonAsync(
            error,
            cancellationToken);
    }
}