using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api")]
    public class AuthorMarksController : ControllerBase
    {
        private readonly IAuthorMarkService _authorMarkService;

        public AuthorMarksController(IAuthorMarkService authorMarkService)
        {
            _authorMarkService = authorMarkService ?? throw new ArgumentNullException(nameof(authorMarkService));
        }

        [HttpPost("posts/{postId}/images/{imageId}/author-marks")]
        public async Task<ActionResult<AuthorMarkDto>> Create(int postId, int imageId, [FromBody] CreateAuthorMarkDto dto)
        {
            var userId = User.GetUserIdOrThrow();
            var created = await _authorMarkService.CreateAsync(postId, imageId, userId, dto);
            return Created($"/api/author-marks/{created.Id}", created);
        }

        [HttpPut("author-marks/{markId}")]
        public async Task<ActionResult<AuthorMarkDto>> Update(int markId, [FromBody] UpdateAuthorMarkDto dto)
        {
            var userId = User.GetUserIdOrThrow();
            var updated = await _authorMarkService.UpdateAsync(markId, userId, dto);
            return Ok(updated);
        }

        [HttpDelete("author-marks/{markId}")]
        public async Task<IActionResult> Delete(int markId)
        {
            var userId = User.GetUserIdOrThrow();
            await _authorMarkService.DeleteAsync(markId, userId);
            return NoContent();
        }
    }
}
