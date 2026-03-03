using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Route("api/admin/users")]
    [Authorize(Policy = "RequireStaff")]
    public class AdminUsersController : ControllerBase
    {
        private readonly IUserModerationService _service;

        public AdminUsersController(IUserModerationService service)
        {
            _service = service;
        }

        [HttpPost("{userId}/restrict")]
        public async Task<ActionResult<UserRestrictionDto>> Restrict(string userId, [FromBody] SetUserRestrictionDto dto)
            => Ok(await _service.RestrictAsync(User.GetUserIdOrThrow(), userId, dto));

        [HttpPost("{userId}/lift")]
        public async Task<ActionResult<UserRestrictionDto>> Lift(string userId)
            => Ok(await _service.LiftAsync(User.GetUserIdOrThrow(), userId));

        [HttpPost("{userId}/suspend")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<ActionResult<UserRestrictionDto>> Suspend(string userId, [FromBody] SetSuspensionDto dto)
            => Ok(await _service.SuspendAsync(User.GetUserIdOrThrow(), userId, dto));

        [HttpPost("{userId}/unsuspend")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<ActionResult<UserRestrictionDto>> Unsuspend(string userId)
            => Ok(await _service.UnsuspendAsync(User.GetUserIdOrThrow(), userId));
    }
}
