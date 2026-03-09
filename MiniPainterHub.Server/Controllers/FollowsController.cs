using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class FollowsController : ControllerBase
    {
        private readonly IFollowService _followService;

        public FollowsController(IFollowService followService)
        {
            _followService = followService;
        }

        [HttpPost("{userId}")]
        public async Task<IActionResult> Follow(string userId)
        {
            await _followService.FollowAsync(User.GetUserIdOrThrow(), userId);
            return NoContent();
        }

        [HttpDelete("{userId}")]
        public async Task<IActionResult> Unfollow(string userId)
        {
            await _followService.UnfollowAsync(User.GetUserIdOrThrow(), userId);
            return NoContent();
        }

        [HttpGet("me/followers")]
        public async Task<IActionResult> GetFollowers()
        {
            var users = await _followService.GetFollowersAsync(User.GetUserIdOrThrow());
            return Ok(users);
        }

        [HttpGet("me/following")]
        public async Task<IActionResult> GetFollowing()
        {
            var users = await _followService.GetFollowingAsync(User.GetUserIdOrThrow());
            return Ok(users);
        }
    }
}
