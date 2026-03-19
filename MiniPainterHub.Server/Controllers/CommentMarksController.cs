using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api")]
    public class CommentMarksController : ControllerBase
    {
        private readonly ICommentMarkService _commentMarkService;

        public CommentMarksController(ICommentMarkService commentMarkService)
        {
            _commentMarkService = commentMarkService ?? throw new ArgumentNullException(nameof(commentMarkService));
        }

        [HttpGet("comments/{commentId}/mark")]
        [AllowAnonymous]
        public async Task<ActionResult<CommentMarkDto>> GetByCommentId(int commentId, [FromQuery] bool includeDeleted = false)
        {
            if (includeDeleted && !(User.IsInRole("Admin") || User.IsInRole("Moderator")))
            {
                throw new ForbiddenException("Only moderators and admins can reveal marks for hidden comments.");
            }

            var mark = await _commentMarkService.GetByCommentIdAsync(commentId, includeDeleted);
            return Ok(mark);
        }

        [HttpPut("comments/{commentId}/mark")]
        public async Task<ActionResult<CommentMarkDto>> Upsert(int commentId, [FromBody] ViewerMarkDraftDto dto)
        {
            var userId = User.GetUserIdOrThrow();
            var mark = await _commentMarkService.UpsertAsync(commentId, userId, dto);
            return Ok(mark);
        }

        [HttpDelete("comments/{commentId}/mark")]
        public async Task<IActionResult> Delete(int commentId)
        {
            var userId = User.GetUserIdOrThrow();
            await _commentMarkService.DeleteAsync(commentId, userId);
            return NoContent();
        }
    }
}
