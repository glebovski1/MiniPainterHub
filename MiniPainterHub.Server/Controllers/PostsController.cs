using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    [Route("api/[controller]")]
    [Authorize]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _postService;

        public PostsController(IPostService postService)
        {
            _postService = postService ?? throw new ArgumentNullException(nameof(postService));
        }

        // GET: api/posts?page=1&pageSize=10
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<PagedResult<PostSummaryDto>>> GetAll(
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
            return Ok(dto);
        }

        // POST: api/posts
        // JSON-only create
        [HttpPost]
        public async Task<ActionResult<PostDto>> Create([FromBody] CreatePostDto dto)
        {
            var userId = User.GetUserIdOrThrow();
            var created = await _postService.CreateAsync(userId, dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        // POST: api/posts/with-image
        // Multipart/form-data create with images and optional thumbnails
        [HttpPost("with-image")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<PostDto>> CreateWithImage(
            [FromForm] CreateImagePostDto dto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var userId = User.GetUserIdOrThrow();
            var cancellation = HttpContext.RequestAborted;

            try
            {
                var created = await _postService.CreateWithImagesAsync(userId, dto, cancellation);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (DomainValidationException ex)
            {
                return ValidationProblem(new ValidationProblemDetails(ex.Errors)
                {
                    Title = ex.Message
                });
            }
            catch (ImageTooLargeException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status413PayloadTooLarge,
                    title: "Image too large",
                    detail: ex.Message);
            }
            catch (UnsupportedImageContentTypeException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status415UnsupportedMediaType,
                    title: "Unsupported media type",
                    detail: ex.Message);
            }
        }

        // PUT: api/posts/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdatePostDto dto)
        {
            var userId = User.GetUserIdOrThrow();
            var updated = await _postService.UpdateAsync(id, userId, dto);
            return NoContent();
        }

        // DELETE: api/posts/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.GetUserIdOrThrow();
            var deleted = await _postService.DeleteAsync(id, userId);
            return NoContent();
        }
    }
}

