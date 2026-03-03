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
    [Route("api/admin/feed-policies")]
    [Authorize(Policy = "RequireAdmin")]
    public class AdminFeedPoliciesController : ControllerBase
    {
        private readonly IFeedPolicyAdminService _service;

        public AdminFeedPoliciesController(IFeedPolicyAdminService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<List<FeedPolicyDto>>> Get() => Ok(await _service.GetAllAsync());

        [HttpPost]
        public async Task<ActionResult<FeedPolicyDto>> Create([FromBody] UpsertFeedPolicyDto dto)
            => Ok(await _service.CreateAsync(User.GetUserIdOrThrow(), dto));

        [HttpPut("{id}")]
        public async Task<ActionResult<FeedPolicyDto>> Update(int id, [FromBody] UpsertFeedPolicyDto dto)
            => Ok(await _service.UpdateAsync(User.GetUserIdOrThrow(), id, dto));

        [HttpPut("{id}/activate")]
        public async Task<IActionResult> Activate(int id)
        {
            await _service.ActivateAsync(User.GetUserIdOrThrow(), id);
            return NoContent();
        }
    }
}
