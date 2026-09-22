using System.Text;
using Caygnus.ResumableConversation.Api.Controllers;
using Caygnus.ResumableConversation.Api.Data;
using Caygnus.ResumableConversation.Api.Entities;
using Caygnus.ResumableConversation.Api.Enums;
using Caygnus.ResumableConversation.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Caygnus.Tests;

public class RunStreamControllerTests
{
    [Fact]
    public async Task UnknownRun_Returns404()
    {
        await using var db = CreateDb();

        var controller = CreateController(
            db,
            new EventStreamBroker());

        await controller.Stream(
            Guid.NewGuid(),
            0,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status404NotFound,
            controller.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task NegativeCursor_Returns400()
    {
        await using var db = CreateDb();

        var runId = Guid.NewGuid();

        db.Runs.Add(CreateRun(runId));
        await db.SaveChangesAsync();

        var controller = CreateController(
            db,
            new EventStreamBroker());

        await controller.Stream(
            runId,
            -1,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            controller.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task StaleCursor_Returns409()
    {
        await using var db = CreateDb();

        var runId = Guid.NewGuid();

        db.Runs.Add(CreateRun(runId));

        db.RunEvents.AddRange(
            CreateEvent(runId, 5, "token", "five"),
            CreateEvent(runId, 6, "run.completed", null));

        await db.SaveChangesAsync();

        var controller = CreateController(
            db,
            new EventStreamBroker());

        await controller.Stream(
            runId,
            1,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status409Conflict,
            controller.HttpContext.Response.StatusCode);

        var body = GetBody(controller);

        Assert.Contains(
            "stale_cursor",
            body);

        Assert.Contains(
            "\"earliestAvailableSequence\":5",
            body);
    }

    [Fact]
    public async Task ReplayFromCursor_ReturnsOnlyEventsAfterCursor()
    {
        await using var db = CreateDb();

        var runId = Guid.NewGuid();

        db.Runs.Add(CreateRun(runId));

        db.RunEvents.AddRange(
            CreateEvent(runId, 1, "run.started", null),
            CreateEvent(runId, 2, "token", "Hello"),
            CreateEvent(runId, 3, "token", " world"),
            CreateEvent(runId, 4, "run.completed", null));

        await db.SaveChangesAsync();

        var controller = CreateController(
            db,
            new EventStreamBroker());

        await controller.Stream(
            runId,
            2,
            CancellationToken.None);

        var body = GetBody(controller);

        Assert.DoesNotContain(
            "id: 1\n",
            
            body);

        Assert.DoesNotContain(
            "id: 2\n",
            body);

        Assert.Contains(
            "id: 3\n",
            body);

        Assert.Contains(
            "id: 4\n",
            body);
    }

    [Fact]
    public async Task ReplayContainingTerminalEvent_CompletesImmediately()
    {
        await using var db = CreateDb();

        var runId = Guid.NewGuid();

        db.Runs.Add(CreateRun(runId));

        db.RunEvents.Add(
            CreateEvent(
                runId,
                1,
                "run.completed",
                null));

        await db.SaveChangesAsync();

        var controller = CreateController(
            db,
            new EventStreamBroker());

        await controller.Stream(
            runId,
            0,
            CancellationToken.None);

        var body = GetBody(controller);

        Assert.Contains(
            "id: 1\n",
            body);

        Assert.Contains(
            "event: run.completed",
            body);
    }

    private static RunStreamController CreateController(
        AppDbContext db,
        EventStreamBroker broker)
    {
        var controller = new RunStreamController(
            db,
            broker);

        var httpContext = new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            }
        };

        controller.ControllerContext =
            new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = httpContext
            };

        return controller;
    }

    private static string GetBody(
        RunStreamController controller)
    {
        var stream =
            (MemoryStream)controller.HttpContext.Response.Body;

        return Encoding.UTF8.GetString(
            stream.ToArray());
    }

    private static AppDbContext CreateDb()
    {
        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        return new AppDbContext(options);
    }

    private static Run CreateRun(Guid runId)
    {
        var conversationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        return new Run
        {
            Id = runId,
            ConversationId = conversationId,
            UserMessageId = messageId,
            Status = RunStatus.Running,
            StartedAt = DateTimeOffset.UtcNow,
            UserMessage = new Message
            {
                Id = messageId,
                ConversationId = conversationId,
                Role = MessageRole.User,
                Content = "test",
                CreatedAt = DateTimeOffset.UtcNow
            }
        };
    }

    private static RunEvent CreateEvent(
        Guid runId,
        long sequence,
        string eventType,
        string? text)
    {
        return new RunEvent
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Sequence = sequence,
            EventType = eventType,
            Text = text,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}