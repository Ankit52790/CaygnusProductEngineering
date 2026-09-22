using Caygnus.ResumableConversation.Api.Data;
using Caygnus.ResumableConversation.Api.DTOs;
using Caygnus.ResumableConversation.Api.Entities;
using Caygnus.ResumableConversation.Api.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Caygnus.ResumableConversation.Api.Services;

namespace Caygnus.ResumableConversation.Api.Controllers;

[ApiController]
[Route("api/conversations/{conversationId:guid}/messages")]
public class MessageController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly RunProcessor _runProcessor;

    public MessageController(
        AppDbContext dbContext,
        RunProcessor runProcessor)
    {
        _dbContext = dbContext;
        _runProcessor = runProcessor;
    }

    [HttpPost]
    public async Task<ActionResult<SendMessageResponse>> SendMessage(
        Guid conversationId,
        [FromBody] SendMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("Message content is required.");
        }

        var conversation = await _dbContext.Conversations
            .FirstOrDefaultAsync(x => x.Id == conversationId);

        if (conversation == null)
        {
            return NotFound("Conversation not found.");
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = MessageRole.User,
            Content = request.Content.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        var run = new Run
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            UserMessageId = message.Id,
            Status = RunStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Messages.Add(message);
        _dbContext.Runs.Add(run);

        await _dbContext.SaveChangesAsync();

        _ = Task.Run(() => _runProcessor.ProcessAsync(run.Id));

        return Ok(new SendMessageResponse
        {
            ConversationId = conversationId,
            MessageId = message.Id,
            RunId = run.Id
        });
    }
}
