using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Infrastructure.Caching;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Infrastructure.RateLimiting;
using MiniPainterHub.Server.Services.Interfaces;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/projects")]
public sealed class HobbyProjectsController : ControllerBase
{
    private readonly IHobbyProjectService _projectService;

    public HobbyProjectsController(IHobbyProjectService projectService) => _projectService = projectService;

    [HttpGet]
    [AllowAnonymous]
    [OutputCache(PolicyName = OutputCachePolicies.PublicDatabaseShort)]
    public async Task<ActionResult<PagedResult<HobbyProjectSummaryDto>>> GetAll([FromQuery] HobbyProjectQueryDto query) =>
        Ok(await _projectService.GetAllAsync(query));

    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<HobbyProjectSummaryDto>>> GetMine([FromQuery] HobbyProjectQueryDto query) =>
        Ok(await _projectService.GetMineAsync(User.GetUserIdOrThrow(), query));

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<HobbyProjectDto>> GetById(int id) =>
        Ok(await _projectService.GetByIdAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier)));

    [HttpGet("{id:int}/diary")]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<HobbyProjectEntryDto>>> GetDiary(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10) =>
        Ok(await _projectService.GetDiaryAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier), page, pageSize));

    [HttpGet("{id:int}/showcase")]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<HobbyProjectEntryDto>>> GetShowcase(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 24) =>
        Ok(await _projectService.GetShowcaseAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier), page, pageSize));

    [HttpGet("{id:int}/available-posts")]
    public async Task<ActionResult<PagedResult<PostSummaryDto>>> GetAvailablePosts(int id, [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 12) =>
        Ok(await _projectService.GetAvailablePostsAsync(User.GetUserIdOrThrow(), id, search, page, pageSize));

    [HttpPost]
    [EnableRateLimiting(RateLimitingPolicies.Write)]
    public async Task<ActionResult<HobbyProjectDto>> Create([FromBody] CreateHobbyProjectDto request)
    {
        var project = await _projectService.CreateAsync(User.GetUserIdOrThrow(), request);
        return CreatedAtAction(nameof(GetById), new { id = project.Id }, project);
    }

    [HttpPut("{id:int}")]
    [EnableRateLimiting(RateLimitingPolicies.Write)]
    public async Task<ActionResult<HobbyProjectDto>> Update(int id, [FromBody] UpdateHobbyProjectDto request) => Ok(await _projectService.UpdateAsync(User.GetUserIdOrThrow(), id, request));

    [HttpPut("{id:int}/status")]
    [EnableRateLimiting(RateLimitingPolicies.Write)]
    public async Task<ActionResult<HobbyProjectDto>> UpdateStatus(int id, [FromBody] UpdateHobbyProjectStatusDto request) => Ok(await _projectService.UpdateStatusAsync(User.GetUserIdOrThrow(), id, request));

    [HttpPost("{id:int}/archive")]
    [EnableRateLimiting(RateLimitingPolicies.Write)]
    public async Task<ActionResult<HobbyProjectDto>> Archive(int id) => Ok(await _projectService.ArchiveAsync(User.GetUserIdOrThrow(), id));

    [HttpPost("{id:int}/restore")]
    [EnableRateLimiting(RateLimitingPolicies.Write)]
    public async Task<ActionResult<HobbyProjectDto>> Restore(int id) => Ok(await _projectService.RestoreAsync(User.GetUserIdOrThrow(), id));

    [HttpPost("{id:int}/posts")]
    [EnableRateLimiting(RateLimitingPolicies.Write)]
    public async Task<ActionResult<HobbyProjectDto>> LinkPost(int id, [FromBody] LinkHobbyProjectPostDto request) => Ok(await _projectService.LinkPostAsync(User.GetUserIdOrThrow(), id, request));

    [HttpPut("{id:int}/posts/{postId:int}")]
    [EnableRateLimiting(RateLimitingPolicies.Write)]
    public async Task<ActionResult<HobbyProjectDto>> UpdateEntry(int id, int postId, [FromBody] UpdateHobbyProjectEntryDto request) => Ok(await _projectService.UpdateEntryAsync(User.GetUserIdOrThrow(), id, postId, request));

    [HttpDelete("{id:int}/posts/{postId:int}")]
    [EnableRateLimiting(RateLimitingPolicies.Write)]
    public async Task<ActionResult<HobbyProjectDto>> UnlinkPost(int id, int postId) => Ok(await _projectService.UnlinkPostAsync(User.GetUserIdOrThrow(), id, postId));

    [HttpPut("{id:int}/showcase")]
    [EnableRateLimiting(RateLimitingPolicies.Write)]
    public async Task<ActionResult<HobbyProjectDto>> UpdateShowcase(int id, [FromBody] UpdateHobbyProjectShowcaseDto request) => Ok(await _projectService.UpdateShowcaseAsync(User.GetUserIdOrThrow(), id, request));

    [HttpPut("{id:int}/cover")]
    [EnableRateLimiting(RateLimitingPolicies.Write)]
    public async Task<ActionResult<HobbyProjectDto>> UpdateCover(int id, [FromBody] UpdateHobbyProjectCoverDto request) => Ok(await _projectService.UpdateCoverAsync(User.GetUserIdOrThrow(), id, request));
}
