using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
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

            public LikesController(ILikeService likeService)
            {
                _likeService = likeService;
            }

            // GET: api/posts/{postId}/likes
            // Returns total count and whether current user has liked
            [HttpGet]
            public async Task<ActionResult<LikeDto>> GetLikes(int postId)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var count = await _likeService.GetCountAsync(postId);
                var isLiked = await _likeService.IsLikedAsync(postId, userId);
                return Ok(new LikeDto { PostId = postId, Count = count, IsLiked = isLiked });
            }

            // POST: api/posts/{postId}/likes
            // Adds a like (or toggles if desired)
            [HttpPost]
            public async Task<IActionResult> Like(int postId)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var result = await _likeService.ToggleAsync(postId, userId);
                if (!result)
                    return NotFound();  // post not found or error
                return NoContent();     // 204 on success
            }

            // DELETE: api/posts/{postId}/likes
            // Removes a like
            [HttpDelete]
            public async Task<IActionResult> Unlike(int postId)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var result = await _likeService.RemoveAsync(postId, userId);
                if (!result)
                    return NotFound();  // like not found or error
                return NoContent();
            }
        }
    
}
