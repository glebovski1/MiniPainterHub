using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MiniPainterHub.Common.Auth;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Identity;
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
            if (!ModelState.IsValid)
            {
                throw new DomainValidationException("Registration request is invalid.", CreateModelStateErrors());
            }

            var user = new ApplicationUser { UserName = dto.UserName, Email = dto.Email };
            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
            {
                throw new DomainValidationException("Registration failed.", CreateIdentityErrors(result.Errors));
            }

            var createProfileDto = new CreateUserProfileDto
            {
                DisplayName = dto.UserName,
                Bio = string.Empty
            };
            await _profileService.CreateAsync(user.Id, createProfileDto);

            return Ok(new AuthResponseDto { IsSuccess = true });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid)
            {
                throw new DomainValidationException("Login request is invalid.", CreateModelStateErrors());
            }

            var user = await _userManager.FindByNameAsync(dto.UserName);
            if (user == null)
            {
                throw new UnauthorizedAccessException("Invalid username or password.");
            }

            var result = await _signInManager.PasswordSignInAsync(dto.UserName, dto.Password,
                                                                 isPersistent: false,
                                                                 lockoutOnFailure: false);

            if (!result.Succeeded)
            {
                throw new UnauthorizedAccessException("Invalid username or password.");
            }

            var jwtSection = _config.GetSection("Jwt");
            var keyString = jwtSection["Key"]!;
            var issuer = jwtSection["Issuer"]!;
            var audience = jwtSection["Audience"]!;
            var expiryMin = int.Parse(jwtSection["ExpiryMinutes"]!);

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[] {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName),
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

        private Dictionary<string, string[]> CreateModelStateErrors()
        {
            var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in ModelState)
            {
                var messages = kvp.Value?.Errors
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage)
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => m!)
                    .ToArray();

                if (messages is { Length: > 0 })
                {
                    var key = string.IsNullOrWhiteSpace(kvp.Key) ? "Request" : kvp.Key;
                    errors[key] = messages;
                }
            }

            return errors;
        }

        private static IDictionary<string, string[]> CreateIdentityErrors(IEnumerable<IdentityError> errors)
        {
            var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in errors.GroupBy(e => string.IsNullOrWhiteSpace(e.Code) ? "Identity" : e.Code))
            {
                var messages = group
                    .Select(e => string.IsNullOrWhiteSpace(e.Description) ? "Identity error." : e.Description)
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => m!)
                    .ToArray();

                if (messages.Length > 0)
                {
                    result[group.Key] = messages;
                }
            }

            return result;
        }
    }
}
