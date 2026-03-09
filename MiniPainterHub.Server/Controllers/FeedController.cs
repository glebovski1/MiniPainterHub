using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class FeedController : ControllerBase
    {
        private readonly IPostService _postService;

        public FeedController(IPostService postService)
        {
            _postService = postService;
        }

        [HttpGet("following")]
        public async Task<ActionResult<PagedResult<PostSummaryDto>>> GetFollowingFeed(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _postService.GetFollowingFeedAsync(User.GetUserIdOrThrow(), page, pageSize);
            return Ok(result);
        }
    }
}
