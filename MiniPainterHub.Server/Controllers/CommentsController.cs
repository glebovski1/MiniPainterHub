using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Authorize]
    public class CommentsController : Controller
    {
        private readonly ICommentService _commentService;

        public CommentsController(ICommentService commentService)
        {
            _commentService = commentService;
        }

        // GET: api/posts/{postId}/comments?page=1&pageSize=10
        [HttpGet("api/posts/{postId}/comments")]
        public async Task<ActionResult<PagedResult<CommentDto>>> GetByPost(
            int postId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _commentService.GetByPostIdAsync(postId, page, pageSize);
            return Ok(result);
        }

        // POST: api/posts/{postId}/comments
        [HttpPost("api/posts/{postId}/comments")]
        public async Task<ActionResult<CommentDto>> Create(
            int postId,
            [FromBody] CreateCommentDto dto)
        {
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? throw new InvalidOperationException("No user ID in token");
            var created = await _commentService.CreateAsync(userId, postId, dto);
            if (created == null)
                return NotFound();
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        // GET: api/comments/{id}
        [HttpGet("api/comments/{id}")]
        public async Task<ActionResult<CommentDto>> GetById(int id)
        {
            // reuse service by fetching all for parent, then find? Or add a GetById in service?
            // For now, fetch via GetByPost then filter:
            // Alternatively, implement GetByIdAsync in service.
            return NotFound();
        }

        // PUT: api/comments/{id}
        [HttpPut("api/comments/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateCommentDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var updated = await _commentService.UpdateAsync(id, userId, dto);
            if (!updated)
                return NotFound();
            return NoContent();
        }

        // DELETE: api/comments/{id}
        [HttpDelete("api/comments/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            var deleted = await _commentService.DeleteAsync(id, userId, isAdmin);
            if (!deleted)
                return NotFound();
            return NoContent();
        }
    }
}
