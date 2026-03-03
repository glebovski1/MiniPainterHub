using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Route("api/admin/flags")]
    [Authorize(Policy = "RequireAdmin")]
    public class AdminFlagsController : ControllerBase
    {
        private readonly IFeatureFlagsService _flags;

        public AdminFlagsController(IFeatureFlagsService flags)
        {
            _flags = flags;
        }

        [HttpGet]
        public async Task<ActionResult<List<AppSettingDto>>> Get()
            => Ok((await _flags.GetFlagsAsync()).Select(x => new AppSettingDto { Key = x.Key, Value = x.Value }).ToList());

        [HttpPut]
        public async Task<IActionResult> Set([FromBody] SetAppSettingDto dto)
        {
            await _flags.SetFlagAsync(dto.Key, dto.Value, User.GetUserIdOrThrow());
            return NoContent();
        }
    }
}
