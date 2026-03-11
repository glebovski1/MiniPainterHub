using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public sealed class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpPost("posts/{postId:int}")]
        public async Task<IActionResult> ReportPost(int postId, [FromBody] CreateReportRequestDto request)
        {
            await _reportService.SubmitPostReportAsync(User.GetUserIdOrThrow(), postId, request);
            return NoContent();
        }

        [HttpPost("comments/{commentId:int}")]
        public async Task<IActionResult> ReportComment(int commentId, [FromBody] CreateReportRequestDto request)
        {
            await _reportService.SubmitCommentReportAsync(User.GetUserIdOrThrow(), commentId, request);
            return NoContent();
        }

        [HttpPost("users/{userId}")]
        public async Task<IActionResult> ReportUser(string userId, [FromBody] CreateReportRequestDto request)
        {
            await _reportService.SubmitUserReportAsync(User.GetUserIdOrThrow(), userId, request);
            return NoContent();
        }

        [HttpGet]
        [Authorize(Roles = "Moderator,Admin")]
        public async Task<ActionResult<PagedResult<ReportQueueItemDto>>> GetQueue([FromQuery] ReportQueueQueryDto query)
        {
            return Ok(await _reportService.GetQueueAsync(query));
        }

        [HttpPost("{reportId:long}/resolve")]
        [Authorize(Roles = "Moderator,Admin")]
        public async Task<IActionResult> Resolve(long reportId, [FromBody] ResolveReportRequestDto request)
        {
            await _reportService.ResolveAsync(User.GetUserIdOrThrow(), reportId, request);
            return NoContent();
        }
    }
}
