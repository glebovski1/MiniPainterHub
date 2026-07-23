using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Infrastructure.Caching;
using MiniPainterHub.Server.Infrastructure.RateLimiting;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/news")]
[OutputCache(PolicyName = OutputCachePolicies.PublicDatabaseShort)]
public sealed class NewsAnnouncementsController : ControllerBase
{
    private readonly INewsAnnouncementService _newsAnnouncementService;

    public NewsAnnouncementsController(INewsAnnouncementService newsAnnouncementService)
    {
        _newsAnnouncementService = newsAnnouncementService ?? throw new ArgumentNullException(nameof(newsAnnouncementService));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<NewsAnnouncementSummaryDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _newsAnnouncementService.GetAllAsync(page, pageSize);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<NewsAnnouncementDto>> GetById(int id)
    {
        var result = await _newsAnnouncementService.GetByIdAsync(id);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting(RateLimitingPolicies.Write)]
    public async Task<ActionResult<NewsAnnouncementDto>> Create([FromBody] CreateNewsAnnouncementDto dto)
    {
        var userId = User.GetUserIdOrThrow();
        var created = await _newsAnnouncementService.CreateAsync(userId, dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
