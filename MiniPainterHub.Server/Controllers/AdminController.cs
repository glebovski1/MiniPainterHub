using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize]
    public sealed class AdminController : ControllerBase
    {
        private readonly IAdminInboxService _inboxService;
        private readonly IAdminSiteControlService _siteControlService;
        private readonly IAdminDashboardService _dashboardService;

        public AdminController(
            IAdminInboxService inboxService,
            IAdminSiteControlService siteControlService,
            IAdminDashboardService dashboardService)
        {
            _inboxService = inboxService;
            _siteControlService = siteControlService;
            _dashboardService = dashboardService;
        }

        [HttpGet("inbox")]
        [Authorize(Roles = "Moderator,Admin")]
        public async Task<ActionResult<PagedResult<AdminInboxItemDto>>> GetInbox([FromQuery] AdminInboxQueryDto query) =>
            Ok(await _inboxService.GetInboxAsync(query));

        [HttpGet("inbox/{targetType}/{targetId}")]
        [Authorize(Roles = "Moderator,Admin")]
        public async Task<ActionResult<AdminInboxDetailDto>> GetInboxDetail(string targetType, string targetId) =>
            Ok(await _inboxService.GetDetailAsync(targetType, targetId));

        [HttpPost("inbox/{targetType}/{targetId}/review")]
        [Authorize(Roles = "Moderator,Admin")]
        public async Task<IActionResult> ReviewInboxItem(string targetType, string targetId, [FromBody] AdminInboxReviewRequestDto request)
        {
            await _inboxService.ReviewAsync(targetType, targetId, User.GetUserIdOrThrow(), request);
            return NoContent();
        }

        [HttpGet("controls")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IReadOnlyList<AdminSiteControlDto>>> GetControls() =>
            Ok(await _siteControlService.GetControlsAsync());

        [HttpPut("controls/{key}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<AdminSiteControlDto>> UpdateControl(string key, [FromBody] UpdateAdminSiteControlRequestDto request) =>
            Ok(await _siteControlService.UpdateControlAsync(key, request, User.GetUserIdOrThrow()));

        [HttpGet("dashboard")]
        [Authorize(Roles = "Moderator,Admin")]
        public async Task<ActionResult<AdminDashboardStatsDto>> GetDashboard([FromQuery] int windowHours = 24) =>
            Ok(await _dashboardService.GetStatsAsync(windowHours));
    }
}
