using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProfilesController : Controller
    {
        private readonly IProfileService _profiles;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfilesController(IProfileService profiles, UserManager<ApplicationUser> userManager)
        {
            _profiles = profiles;
            _userManager = userManager;
        }

        [HttpGet("me")]
        public async Task<ActionResult<ProfileDto>> GetMyProfile()
        {

            var userName = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userName))
                return Unauthorized();

            // 2️⃣ Look up the user record to get the Id
            var user = await _userManager.FindByNameAsync(userName);
            if (user == null)
                return Unauthorized();

            // 3️⃣ Now fetch the profile by user.Id
            var dto = await _profiles.GetByUserIdAsync(user.Id);
            return dto == null ? NotFound() : Ok(dto);
        }

        [HttpPut("me")]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userName = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userName))
                return Unauthorized();

            // 2️⃣ Look up the user record to get the Id
            var user = await _userManager.FindByNameAsync(userName);
            if (user == null)
                return Unauthorized();

            var updatedProfile = await _profiles.UpdateAsync(user.Id, dto);

            if (updatedProfile == null)
                return NotFound();          // 404 if there was no profile to update

            return Ok(updatedProfile);
        }
    }
}
