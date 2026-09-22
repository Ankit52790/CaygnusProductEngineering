using Caygnus.ResumableConversation.Api.Data;
using Caygnus.ResumableConversation.Api.DTOs;
using Caygnus.ResumableConversation.Api.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Caygnus.ResumableConversation.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConversationController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public ConversationController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpPost]
        public async Task<ActionResult<CreateConversationResponse>> CreateConversation()
        {
            var conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.Conversations.Add(conversation);

            await _dbContext.SaveChangesAsync();

            return Ok(new CreateConversationResponse
            {
                ConversationId = conversation.Id
            });
        }
    }
}
