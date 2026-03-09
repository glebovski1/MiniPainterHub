using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Services.Interfaces; // IProfileService
using System;
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

    public ProfilesController(IProfileService profiles)
        => _profilesService = profiles;

    [HttpGet("me")]
    public async Task<ActionResult<UserProfileDto>> GetMyProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("User is not authenticated.");

        var dto = await _profilesService.GetByUserIdAsync(userId)
                  ?? throw new NotFoundException("Profile not found.");
        return Ok(dto);
    }

    [HttpPost("me")]
    public async Task<ActionResult<UserProfileDto>> CreateMyProfile([FromBody] CreateUserProfileDto body)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("User is not authenticated.");

        var created = await _profilesService.CreateAsync(userId, body);
        return CreatedAtAction(nameof(GetMyProfile), null, created);
    }

    [HttpPut("me")]
    public async Task<ActionResult<UserProfileDto>> UpdateMyProfile([FromBody] UpdateUserProfileDto body)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("User is not authenticated.");

        var updated = await _profilesService.UpdateAsync(userId, body);
        return Ok(updated);
    }

    // === AVATAR UPLOAD: single URL per user, always overwritten ===
    [HttpPost("me/avatar")]
    [RequestSizeLimit(5_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UserProfileDto>> UploadAvatar([FromForm] IFormFile file)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("User is not authenticated.");

        var updated = await _profilesService.UploadAvatarAsync(userId, file);
        return Ok(updated);
    }

    [HttpDelete("me/avatar")]
    public async Task<ActionResult<UserProfileDto>> RemoveAvatar()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("User is not authenticated.");

        var updated = await _profilesService.RemoveAvatarAsync(userId);
        return Ok(updated);
    }

    [AllowAnonymous]
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<UserProfileDto>> GetUserProfileById(string id)
    {
        var viewerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var dto = await _profilesService.GetUserProfileById(id, viewerUserId);
        return Ok(dto);
    }
}
