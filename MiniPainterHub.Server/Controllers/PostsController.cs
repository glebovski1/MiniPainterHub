using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _postService;
        private readonly IImageService _imageService;

        public PostsController(
            IPostService postService,
            IImageService imageService)
        {
            _postService = postService;
            _imageService = imageService;
        }

        // GET: api/posts?page=1&pageSize=10
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<PagedResult<PostDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _postService.GetAllAsync(page, pageSize);
            return Ok(result);
        }

        // GET: api/posts/5
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<PostDto>> GetById(int id)
        {
            var dto = await _postService.GetByIdAsync(id);
            if (dto == null)
                return NotFound();
            return Ok(dto);
        }

        // POST: api/posts
        // JSON-only create
        [HttpPost]
        public async Task<ActionResult<PostDto>> Create([FromBody] CreatePostDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var created = await _postService.CreateAsync(userId, dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        // POST: api/posts/with-image
        // Multipart/form-data create with image
        [HttpPost("with-image")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<PostDto>> CreateWithImage(
            [FromForm] CreateImagePostDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // 1️⃣ create post without imageUrl
            var created = await _postService.CreateAsync(userId, new CreatePostDto
            {
                Title = dto.Title,
                Content = dto.Content
            });

            // 2️⃣ handle image upload
            if (dto.Image is { Length: > 0 })
            {
                using var stream = dto.Image.OpenReadStream();
                var fileName = $"{created.Id}_{dto.Image.FileName}";
                var url = await _imageService.UploadAsync(stream, fileName);
                await _postService.SetImageUrlAsync(created.Id, url);
                created.ImageUrl = url;
            }

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        // PUT: api/posts/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdatePostDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var updated = await _postService.UpdateAsync(id, userId, dto);
            if (!updated)
                return NotFound();
            return NoContent();
        }

        // DELETE: api/posts/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var deleted = await _postService.DeleteAsync(id, userId);
            if (!deleted)
                return NotFound();
            return NoContent();
        }
    }
}

