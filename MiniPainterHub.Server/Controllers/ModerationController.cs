using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ModerationController : ControllerBase
    {
        private readonly IModerationService _moderationService;

        public ModerationController(IModerationService moderationService)
        {
            _moderationService = moderationService;
        }

        [HttpPost("posts/{postId:int}/hide")]
        [Authorize(Roles = "Moderator,Admin")]
        public async Task<IActionResult> HidePost(int postId, [FromBody] ModerationActionRequestDto dto)
        {
            await _moderationService.ModeratePostAsync(postId, User.GetUserIdOrThrow(), true, dto.Reason);
            return NoContent();
        }

        [HttpPost("posts/{postId:int}/restore")]
        [Authorize(Roles = "Moderator,Admin")]
        public async Task<IActionResult> RestorePost(int postId, [FromBody] ModerationActionRequestDto dto)
        {
            await _moderationService.ModeratePostAsync(postId, User.GetUserIdOrThrow(), false, dto.Reason);
            return NoContent();
        }

        [HttpPost("comments/{commentId:int}/hide")]
        [Authorize(Roles = "Moderator,Admin")]
        public async Task<IActionResult> HideComment(int commentId, [FromBody] ModerationActionRequestDto dto)
        {
            await _moderationService.ModerateCommentAsync(commentId, User.GetUserIdOrThrow(), true, dto.Reason);
            return NoContent();
        }

        [HttpPost("comments/{commentId:int}/restore")]
        [Authorize(Roles = "Moderator,Admin")]
        public async Task<IActionResult> RestoreComment(int commentId, [FromBody] ModerationActionRequestDto dto)
        {
            await _moderationService.ModerateCommentAsync(commentId, User.GetUserIdOrThrow(), false, dto.Reason);
            return NoContent();
        }

        [HttpPost("users/{userId}/suspend")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SuspendUser(string userId, [FromBody] SuspendUserRequestDto dto)
        {
            await _moderationService.SuspendUserAsync(userId, User.GetUserIdOrThrow(), dto.SuspendedUntilUtc, dto.Reason);
            return NoContent();
        }

        [HttpPost("users/{userId}/unsuspend")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UnsuspendUser(string userId, [FromBody] ModerationActionRequestDto dto)
        {
            await _moderationService.UnsuspendUserAsync(userId, User.GetUserIdOrThrow(), dto.Reason);
            return NoContent();
        }

        [HttpGet("audit")]
        [Authorize(Roles = "Moderator,Admin")]
        public async Task<ActionResult<PagedResult<ModerationAuditDto>>> GetAudit([FromQuery] ModerationAuditQueryDto query)
        {
            return Ok(await _moderationService.GetAuditAsync(query));
        }

        [HttpGet("users/lookup")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IReadOnlyList<ModerationUserLookupDto>>> SearchUsers([FromQuery] string? query, [FromQuery] int limit = 10)
        {
            return Ok(await _moderationService.SearchUsersAsync(query, limit));
        }

        [HttpGet("posts/{postId:int}/preview")]
        [Authorize(Roles = "Moderator,Admin")]
        public async Task<ActionResult<ModerationPostPreviewDto>> GetPostPreview(int postId)
        {
            return Ok(await _moderationService.GetPostPreviewAsync(postId));
        }

        [HttpGet("comments/{commentId:int}/preview")]
        [Authorize(Roles = "Moderator,Admin")]
        public async Task<ActionResult<ModerationCommentPreviewDto>> GetCommentPreview(int commentId)
        {
            return Ok(await _moderationService.GetCommentPreviewAsync(commentId));
        }
    }
}
