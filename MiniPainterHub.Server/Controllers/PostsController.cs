using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services.Interfaces;
using MiniPainterHub.Server.Services.Models;
using System;
using System.IO;
using System.Threading;
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
        private const long MaxUploadBytes = 20L * 1024 * 1024;
        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

        private readonly IPostService _postService;
        private readonly IImageService _imageService;
        private readonly IImageProcessor _imageProcessor;
        private readonly IImageStore _imageStore;
        private readonly ImagesOptions _imageOptions;
        private readonly ILogger<PostsController> _logger;

        public PostsController(
            IPostService postService,
            IImageService imageService,
            IOptions<ImagesOptions> imageOptions,
            IImageProcessor imageProcessor,
            IImageStore imageStore,
            ILogger<PostsController> logger)
        {
            _postService = postService ?? throw new ArgumentNullException(nameof(postService));
            _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
            _imageOptions = imageOptions?.Value ?? throw new ArgumentNullException(nameof(imageOptions));
            _imageProcessor = imageProcessor ?? throw new ArgumentNullException(nameof(imageProcessor));
            _imageStore = imageStore ?? throw new ArgumentNullException(nameof(imageStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                _logger.LogInformation("Processing {Count} uploaded images for post {PostId} (pipeline enabled: {Enabled})", Math.Min(dto.Images.Count, 5), created.Id, _imageOptions.Enabled);

                var imageDtos = new List<PostImageDto>();
                var cancellation = HttpContext.RequestAborted;

                if (!_imageOptions.Enabled)
                {
                    for (int i = 0; i < dto.Images.Count && i < 5; i++)
                    {
                        var img = dto.Images[i];

                        if (img.Length > MaxUploadBytes)
                        {
                            return Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: "Image too large", detail: $"Image '{img.FileName}' exceeds the {MaxUploadBytes / (1024 * 1024)} MB limit.");
                        }

                        await using var stream = img.OpenReadStream(MaxUploadBytes, cancellation);
                        var fileName = $"{created.Id}_{i}_{img.FileName}";
                        var url = await _imageService.UploadAsync(stream, fileName);

                        string? thumbUrl = null;
                        if (dto.Thumbnails != null && i < dto.Thumbnails.Count && dto.Thumbnails[i] is { Length: > 0 } thumb)
                        {
                            await using var thumbStream = thumb.OpenReadStream(MaxUploadBytes, cancellation);
                            var thumbFileName = $"{created.Id}_{i}_thumb_{thumb.FileName}";
                            thumbUrl = await _imageService.UploadAsync(thumbStream, thumbFileName);
                        }

                        imageDtos.Add(new PostImageDto
                        {
                            ImageUrl = url,
                            ThumbnailUrl = thumbUrl
                        });
                    }
                }
                else
                {
                    for (int i = 0; i < dto.Images.Count && i < 5; i++)
                    {
                        var img = dto.Images[i];
                        var contentType = img.ContentType ?? string.Empty;

                        if (img.Length > MaxUploadBytes)
                        {
                            return Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: "Image too large", detail: $"Image '{img.FileName}' exceeds the {MaxUploadBytes / (1024 * 1024)} MB limit.");
                        }

                        if (!AllowedContentTypes.Contains(contentType))
                        {
                            return Problem(statusCode: StatusCodes.Status415UnsupportedMediaType, title: "Unsupported media type", detail: $"Images must be JPEG, PNG, or WebP. '{img.FileName}' was {contentType}.");
                        }

                        await using var stream = img.OpenReadStream(MaxUploadBytes, cancellation);
                        var variants = await _imageProcessor.ProcessAsync(stream, contentType, cancellation);
                        var imageId = Guid.NewGuid();
                        var stored = await _imageStore.SaveAsync(created.Id, imageId, variants, cancellation);

                        imageDtos.Add(new PostImageDto
                        {
                            ImageUrl = stored.MaxUrl,
                            ThumbnailUrl = stored.ThumbUrl
                        });
                    }
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

