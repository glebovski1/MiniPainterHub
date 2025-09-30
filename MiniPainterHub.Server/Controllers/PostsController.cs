using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

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
            if (dto.Images != null && dto.Images.Count > 5)
            {
                throw new DomainValidationException("Invalid post images.", new Dictionary<string, string[]>
                {
                    ["Images"] = new[] { "A maximum of 5 images is allowed." }
                });
            }

            var userId = User.GetUserIdOrThrow();
            // create post first
            var created = await _postService.CreateAsync(userId, new CreatePostDto
            {
                Title = dto.Title,
                Content = dto.Content
            });

            if (dto.Images is { Count: > 0 })
            {
                var imageDtos = new List<PostImageDto>();

                for (int i = 0; i < dto.Images.Count && i < 5; i++)
                {
                    var img = dto.Images[i];
                    using var stream = img.OpenReadStream();
                    var fileName = $"{created.Id}_{i}_{img.FileName}";
                    var url = await _imageService.UploadAsync(stream, fileName);

                    string? thumbUrl = null;
                    if (dto.Thumbnails != null && i < dto.Thumbnails.Count && dto.Thumbnails[i] is { Length: > 0 })
                    {
                        var thumb = dto.Thumbnails[i];
                        using var tStream = thumb.OpenReadStream();
                        var thumbFileName = $"{created.Id}_{i}_thumb_{thumb.FileName}";
                        thumbUrl = await _imageService.UploadAsync(tStream, thumbFileName);
                    }

                    imageDtos.Add(new PostImageDto
                    {
                        ImageUrl = url,
                        ThumbnailUrl = thumbUrl
                    });
                }

                created.Images = await _postService.AddImagesAsync(created.Id, imageDtos);
                created.ImageUrl = created.Images
                                       .OrderBy(i => i.Id)
                                       .Select(i => i.ImageUrl)
                                       .FirstOrDefault();
            }

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
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

