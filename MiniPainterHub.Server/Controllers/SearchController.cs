using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Infrastructure.Caching;
using MiniPainterHub.Server.Infrastructure.RateLimiting;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicies.Search)]
    [OutputCache(PolicyName = OutputCachePolicies.PublicDatabaseShort)]
    public sealed class SearchController : ControllerBase
    {
        private readonly ISearchService _searchService;

        public SearchController(ISearchService searchService)
        {
            _searchService = searchService;
        }

        [HttpGet("overview")]
        public Task<ActionResult<SearchOverviewDto>> GetOverview([FromQuery] string? q, CancellationToken cancellationToken) =>
            SearchAsync(() => _searchService.GetOverviewAsync(q, cancellationToken));

        [HttpGet("posts")]
        public Task<ActionResult<PagedResult<PostSummaryDto>>> SearchPosts(
            [FromQuery] string? q,
            [FromQuery] string? tag,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default) =>
            SearchAsync(() => _searchService.SearchPostsAsync(q, tag, page, pageSize, cancellationToken));

        [HttpGet("projects")]
        public Task<ActionResult<PagedResult<HobbyProjectSummaryDto>>> SearchProjects(
            [FromQuery] string? q,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default) =>
            SearchAsync(() => _searchService.SearchProjectsAsync(q, page, pageSize, cancellationToken));

        [HttpGet("users")]
        public Task<ActionResult<PagedResult<UserListItemDto>>> SearchUsers(
            [FromQuery] string? q,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default) =>
            SearchAsync(() => _searchService.SearchUsersAsync(q, page, pageSize, cancellationToken));

        [HttpGet("tags")]
        public Task<ActionResult<PagedResult<SearchTagResultDto>>> SearchTags(
            [FromQuery] string? q,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default) =>
            SearchAsync(() => _searchService.SearchTagsAsync(q, page, pageSize, cancellationToken));

        private async Task<ActionResult<T>> SearchAsync<T>(Func<Task<T>> action)
        {
            var result = await action();
            return Ok(result);
        }
    }
}
