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
        private readonly IPostService _postService;

        public CommentsController(ICommentService commentService, IPostService postService)
        {
            _commentService = commentService;
            _postService = postService; 
        }

        // GET: api/posts/{postId}/comments?page=1&pageSize=10
        [HttpGet("api/posts/{postId}/comments"), AllowAnonymous]
        public async Task<ActionResult<PagedResult<CommentDto>>> GetByPost(
            int postId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (!await _postService.ExistsAsync(postId))
                return NotFound();
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
            await _commentService.DeleteAsync(id, userId, isAdmin);
            return NoContent();
        }
    }
}
