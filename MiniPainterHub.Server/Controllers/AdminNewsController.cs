using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.DTOs;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Route("api/admin/news")]
    [Authorize(Policy = "RequireAdmin")]
    public class AdminNewsController : ControllerBase
    {
        private readonly INewsAdminService _service;

        public AdminNewsController(INewsAdminService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<List<NewsItemDto>>> Get() => Ok(await _service.GetAllAsync());

        [HttpPost]
        public async Task<ActionResult<NewsItemDto>> Create([FromBody] UpsertNewsItemDto dto)
            => Ok(await _service.CreateAsync(User.GetUserIdOrThrow(), dto));

        [HttpPut("{id}")]
        public async Task<ActionResult<NewsItemDto>> Update(int id, [FromBody] UpsertNewsItemDto dto)
            => Ok(await _service.UpdateAsync(User.GetUserIdOrThrow(), id, dto));

        [HttpPost("{id}/hide")]
        public async Task<IActionResult> Hide(int id, [FromBody] ModerationReasonDto dto)
        {
            await _service.HideAsync(User.GetUserIdOrThrow(), id, dto?.Reason);
            return NoContent();
        }

        [HttpPost("{id}/unhide")]
        public async Task<IActionResult> Unhide(int id, [FromBody] ModerationReasonDto dto)
        {
            await _service.UnhideAsync(User.GetUserIdOrThrow(), id, dto?.Reason);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id, [FromBody] ModerationReasonDto dto)
        {
            await _service.DeleteAsync(User.GetUserIdOrThrow(), id, dto?.Reason);
            return NoContent();
        }
    }
}
