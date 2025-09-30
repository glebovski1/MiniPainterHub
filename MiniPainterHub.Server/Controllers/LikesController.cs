using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers
{
    
        [ApiController]
        [Authorize]
        [Route("api/posts/{postId}/likes")]
        public class LikesController : ControllerBase
        {
            private readonly ILikeService _likeService;
            private readonly IPostService _postService;

            public LikesController(ILikeService likeService, IPostService postService)
            {
                _likeService = likeService;
                _postService = postService;
            }

            // GET: api/posts/{postId}/likes
            // Returns total count and whether current user has liked
            [HttpGet, AllowAnonymous]
            public async Task<ActionResult<LikeDto>> GetLikes(int postId)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var likeDto = await _likeService.GetLikesAsync(postId, userId);
                return Ok(likeDto);
            }

            // POST: api/posts/{postId}/likes
            // Adds a like (or toggles if desired)
            [HttpPost]
            public async Task<IActionResult> Like(int postId)
            {
                var userId = User.GetUserIdOrThrow();
                await _likeService.ToggleAsync(postId, userId);
                return NoContent();
            }

            // DELETE: api/posts/{postId}/likes
            // Removes a like
            [HttpDelete]
            public async Task<IActionResult> Unlike(int postId)
            {
                var userId = User.GetUserIdOrThrow();
                await _likeService.RemoveAsync(postId, userId);
                return NoContent();
            }
        }
    
}
