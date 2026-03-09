using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class ConversationsController : ControllerBase
    {
        private readonly IConversationService _conversationService;

        public ConversationsController(IConversationService conversationService)
        {
            _conversationService = conversationService;
        }

        [HttpGet]
        public async Task<ActionResult> GetConversations()
        {
            var conversations = await _conversationService.GetConversationsAsync(User.GetUserIdOrThrow());
            return Ok(conversations);
        }

        [HttpPost("direct/{userId}")]
        public async Task<ActionResult<ConversationSummaryDto>> OpenDirectConversation(string userId)
        {
            var conversation = await _conversationService.GetOrCreateDirectConversationAsync(User.GetUserIdOrThrow(), userId);
            return Ok(conversation);
        }

        [HttpGet("{id:int}/messages")]
        public async Task<ActionResult<PagedResult<DirectMessageDto>>> GetMessages(int id, [FromQuery] int? beforeMessageId = null, [FromQuery] int pageSize = 50)
        {
            var result = await _conversationService.GetMessagesAsync(User.GetUserIdOrThrow(), id, beforeMessageId, pageSize);
            return Ok(result);
        }

        [HttpPost("{id:int}/messages")]
        public async Task<ActionResult<DirectMessageDto>> SendMessage(int id, [FromBody] CreateDirectMessageDto dto)
        {
            var message = await _conversationService.SendMessageAsync(User.GetUserIdOrThrow(), id, dto);
            return Ok(message);
        }

        [HttpPost("{id:int}/read")]
        public async Task<IActionResult> MarkRead(int id)
        {
            await _conversationService.MarkReadAsync(User.GetUserIdOrThrow(), id);
            return NoContent();
        }
    }
}
