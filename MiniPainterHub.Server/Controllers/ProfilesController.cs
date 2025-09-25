using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Services.Interfaces; // IProfileService, IImageService
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ProfilesController : ControllerBase
{
    private readonly IProfileService _profilesService;
    private readonly IImageService _images;

    public ProfilesController(IProfileService profiles, IImageService images)
    {
        _profilesService = profiles;
        _images = images;
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserProfileDto>> GetMyProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var dto = await _profilesService.GetByUserIdAsync(userId);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("me")]
    public async Task<ActionResult<UserProfileDto>> CreateMyProfile([FromBody] CreateUserProfileDto body)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        try
        {
            var created = await _profilesService.CreateAsync(userId, body);
            return CreatedAtAction(nameof(GetMyProfile), null, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            ModelState.AddModelError(ex.ParamName ?? "input", ex.Message);
            return ValidationProblem(ModelState);
        }
    }

    [HttpPut("me")]
    public async Task<ActionResult<UserProfileDto>> UpdateMyProfile([FromBody] UpdateUserProfileDto body)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        try
        {
            var updated = await _profilesService.UpdateAsync(userId, body);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            ModelState.AddModelError(ex.ParamName ?? "input", ex.Message);
            return ValidationProblem(ModelState);
        }
    }

    // === AVATAR UPLOAD: single URL per user, always overwritten ===
    [HttpPost("me/avatar")]
    [RequestSizeLimit(5_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UserProfileDto>> UploadAvatar([FromForm] IFormFile file)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        if (file is null || file.Length == 0) return BadRequest("No file uploaded.");
        if (file.Length > 5_000_000) return BadRequest("Max avatar size is 5 MB.");

        var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowed.Contains(file.ContentType))
            return BadRequest("Only JPEG, PNG, or WEBP images are allowed.");

        // Re-encode to a single format for a stable filename (JPEG here)
        await using var inStream = file.OpenReadStream();
        using var image = await SixLabors.ImageSharp.Image.LoadAsync(inStream);
        const int MAX = 512;
        image.Mutate(x => x.Resize(new SixLabors.ImageSharp.Processing.ResizeOptions
        {
            Mode = SixLabors.ImageSharp.Processing.ResizeMode.Max,
            Size = new SixLabors.ImageSharp.Size(MAX, MAX)
        }));

        await using var outMs = new MemoryStream();
        await image.SaveAsJpegAsync(outMs, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 85 });
        outMs.Position = 0;

        // FIXED filename per user
        var fileName = $"avatar_{userId}.jpg";
        var publicUrl = await _images.UploadAsync(outMs, fileName); // both services overwrite

        // Persist the URL on profile (if first time) or keep same URL (overwritten file)
        var updated = await _profilesService.SetAvatarUrlAsync(userId, publicUrl);
        return Ok(updated);
    }


    [HttpGet("{id}")]
    public async Task<ActionResult<UserProfileDto>> GetUserProfileById(string id)
    {
        try
        {
            var dto = await _profilesService.GetUserProfileById(id);
            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
