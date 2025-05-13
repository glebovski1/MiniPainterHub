using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MiniPainterHub.Common.Auth;
using MiniPainterHub.Server.Identity;
using System.IdentityModel.Tokens.Jwt;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Text;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Services.Interfaces;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _config;
        private readonly IProfileService _profileService;

        public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IProfileService profileService, IConfiguration config)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _config = config;
            _profileService = profileService;
        }


        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var user = new ApplicationUser { UserName = dto.UserName, Email = dto.Email };
            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
                return BadRequest(new AuthResponseDto
                {
                    IsSuccess = false,
                    Errors = result.Errors.Select(e => e.Description)
                });

            var createProfileDto = new CreateProfileDto
            {
                DisplayName = dto.UserName,
                Bio = string.Empty  // or null, whichever your model allows
            };
            await _profileService.CreateAsync(user.Id, createProfileDto);

            // Optionally sign in the user here or return a token directly
            return Ok(new AuthResponseDto { IsSuccess = true });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var result = await _signInManager.PasswordSignInAsync(dto.UserName, dto.Password,
                                                                 isPersistent: false,
                                                                 lockoutOnFailure: false);

            if (!result.Succeeded)
                return Unauthorized(new AuthResponseDto
                {
                    IsSuccess = false,
                    Errors = new[] { "Invalid username or password." }
                });

            var jwtSection = _config.GetSection("Jwt");
            var keyString = jwtSection["Key"]!;
            var issuer = jwtSection["Issuer"]!;
            var audience = jwtSection["Audience"]!;
            var expiryMin = int.Parse(jwtSection["ExpiryMinutes"]!);

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[] {
                new Claim(JwtRegisteredClaimNames.Sub, dto.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };


            var token = new JwtSecurityToken(
                                   issuer: issuer,
                                   audience: audience,
                                   claims: claims,
                                   expires: DateTime.UtcNow.AddMinutes(expiryMin),
                                   signingCredentials: creds);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new AuthResponseDto
            {
                IsSuccess = true,
                Token = tokenString
            });
        }


    }
}
