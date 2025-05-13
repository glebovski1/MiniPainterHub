using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Services.Interfaces;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PostsController : Controller
    {
        private readonly IPostService _postService;

        public PostsController(IPostService postService)
        {
            _postService = postService;
        }

        // GET: api/posts?page=1&pageSize=10
        [HttpGet]
        public async Task<ActionResult<PagedResult<PostDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _postService.GetAllAsync(page, pageSize);
            return Ok(result);
        }

        // GET: api/posts/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PostDto>> GetById(int id)
        {
            var dto = await _postService.GetByIdAsync(id);
            if (dto == null)
                return NotFound();
            return Ok(dto);
        }

        // POST: api/posts
        [HttpPost]
        public async Task<ActionResult<PostDto>> Create([FromBody] CreatePostDto dto)
        {
            var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            var created = await _postService.CreateAsync(userId, dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        // PUT: api/posts/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdatePostDto dto)
        {
            var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            var updated = await _postService.UpdateAsync(id, userId, dto);
            if (!updated)
                return NotFound();
            return NoContent();
        }

        // DELETE: api/posts/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            var deleted = await _postService.DeleteAsync(id, userId);
            if (!deleted)
                return NotFound();
            return NoContent();
        }
    }
}
}
