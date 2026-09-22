using Caygnus.ResumableConversation.Api.Data;
using Caygnus.ResumableConversation.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Caygnus.ResumableConversation.Api.Controllers;


[ApiController]
[Route("api/runs")]
public class RunController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public RunController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("{runId:guid}")]
    public async Task<ActionResult<RunResponse>> GetRun(Guid runId)
    {
        var run = await _dbContext.Runs
            .Include(x => x.Events)
            .FirstOrDefaultAsync(x => x.Id == runId);

        if (run == null)
        {
            return NotFound("Run not found.");
        }

        var response = new RunResponse
        {
            RunId = run.Id,
            ConversationId = run.ConversationId,
            UserMessageId = run.UserMessageId,
            Status = run.Status,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            FailureReason = run.FailureReason,
            Events = run.Events
                .OrderBy(x => x.Sequence)
                .Select(x => new RunEventResponse
                {
                    Id = x.Id,
                    Sequence = x.Sequence,
                    EventType = x.EventType,
                    Text = x.Text,
                    CreatedAt = x.CreatedAt
                })
                .ToList()
        };

        return Ok(response);
    }
}