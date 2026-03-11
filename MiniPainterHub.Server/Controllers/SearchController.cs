using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public sealed class SearchController : ControllerBase
    {
        private readonly ISearchService _searchService;

        public SearchController(ISearchService searchService)
        {
            _searchService = searchService;
        }

        [HttpGet("overview")]
        public Task<ActionResult<SearchOverviewDto>> GetOverview([FromQuery] string? q) =>
            SearchAsync(() => _searchService.GetOverviewAsync(q));

        [HttpGet("posts")]
        public Task<ActionResult<PagedResult<PostSummaryDto>>> SearchPosts(
            [FromQuery] string? q,
            [FromQuery] string? tag,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10) =>
            SearchAsync(() => _searchService.SearchPostsAsync(q, tag, page, pageSize));

        [HttpGet("users")]
        public Task<ActionResult<PagedResult<UserListItemDto>>> SearchUsers(
            [FromQuery] string? q,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10) =>
            SearchAsync(() => _searchService.SearchUsersAsync(q, page, pageSize));

        [HttpGet("tags")]
        public Task<ActionResult<PagedResult<SearchTagResultDto>>> SearchTags(
            [FromQuery] string? q,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10) =>
            SearchAsync(() => _searchService.SearchTagsAsync(q, page, pageSize));

        private async Task<ActionResult<T>> SearchAsync<T>(Func<Task<T>> action)
        {
            var result = await action();
            return Ok(result);
        }
    }
}
