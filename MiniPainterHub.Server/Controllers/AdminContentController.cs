using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Server.DTOs;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System.Threading.Tasks;
using System.Linq;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Route("api/admin/content")]
    [Authorize(Policy = "RequireStaff")]
    public class AdminContentController : ControllerBase
    {
        private readonly IModerationService _moderation;
        private readonly Data.AppDbContext _db;

        public AdminContentController(IModerationService moderation, Data.AppDbContext db)
        {
            _moderation = moderation;
            _db = db;
        }

        [HttpGet("{type}")]
        public async Task<IActionResult> List(string type)
        {
            var normalized = type.ToLowerInvariant();
            if (normalized is "posts" or "post")
            {
                return Ok(await _db.Posts.IgnoreQueryFilters().OrderByDescending(x => x.Id).Take(200).ToListAsync());
            }

            if (normalized is "comments" or "comment")
            {
                return Ok(await _db.Comments.IgnoreQueryFilters().OrderByDescending(x => x.Id).Take(200).ToListAsync());
            }

            if (normalized is "images" or "image")
            {
                return Ok(await _db.PostImages.IgnoreQueryFilters().OrderByDescending(x => x.Id).Take(200).ToListAsync());
            }

            return BadRequest();
        }

        [HttpPost("{type}/{id}/hide")]
        public Task<IActionResult> Hide(string type, int id, [FromBody] ModerationReasonDto dto)
            => Do(async () => await _moderation.HideAsync(User.GetUserIdOrThrow(), type, id, dto?.Reason));

        [HttpPost("{type}/{id}/unhide")]
        public Task<IActionResult> Unhide(string type, int id, [FromBody] ModerationReasonDto dto)
            => Do(async () => await _moderation.UnhideAsync(User.GetUserIdOrThrow(), type, id, dto?.Reason));

        [HttpPost("{type}/{id}/softdelete")]
        public Task<IActionResult> SoftDelete(string type, int id, [FromBody] ModerationReasonDto dto)
            => Do(async () => await _moderation.SoftDeleteAsync(User.GetUserIdOrThrow(), type, id, dto?.Reason));

        [HttpDelete("{type}/{id}")]
        [Authorize(Policy = "RequireAdmin")]
        public Task<IActionResult> HardDelete(string type, int id, [FromBody] ModerationReasonDto dto)
            => Do(async () => await _moderation.HardDeleteAsync(User.GetUserIdOrThrow(), type, id, dto?.Reason));

        private static async Task<IActionResult> Do(System.Func<Task> action)
        {
            await action();
            return new NoContentResult();
        }
    }
}
