using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using MiniPainterHub.Common.Auth;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Infrastructure.RateLimiting;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using MiniPainterHub.Server.Options;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IProfileService _profileService;
        private readonly IAccountRestrictionService _accountRestrictionService;
        private readonly IMaintenanceBypassService _maintenanceBypassService;
        private readonly IJwtTokenIssuer _jwtTokenIssuer;
        private readonly IEmailConfirmationService _emailConfirmation;
        private readonly EmailConfirmationOptions _emailConfirmationOptions;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IProfileService profileService,
            IAccountRestrictionService accountRestrictionService,
            IMaintenanceBypassService maintenanceBypassService,
            IJwtTokenIssuer jwtTokenIssuer,
            IEmailConfirmationService emailConfirmation,
            IOptions<EmailConfirmationOptions> emailConfirmationOptions)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _profileService = profileService;
            _accountRestrictionService = accountRestrictionService;
            _maintenanceBypassService = maintenanceBypassService;
            _jwtTokenIssuer = jwtTokenIssuer;
            _emailConfirmation = emailConfirmation;
            _emailConfirmationOptions = emailConfirmationOptions.Value;
        }

        [HttpPost("register")]
        [EnableRateLimiting(RateLimitingPolicies.EmailConfirmation)]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid)
            {
                throw new DomainValidationException("Registration request is invalid.", CreateModelStateErrors());
            }

            await _accountRestrictionService.EnsureCanRegisterAsync();

            var user = new ApplicationUser
            {
                UserName = dto.UserName,
                Email = dto.Email,
                EmailConfirmed = !_emailConfirmationOptions.Enabled
            };
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

            var confirmationSent = _emailConfirmationOptions.Enabled
                && await _emailConfirmation.SendConfirmationAsync(user, dto.ReturnUrl, HttpContext.RequestAborted);

            return Ok(new RegistrationResultDto
            {
                IsSuccess = true,
                RequiresEmailConfirmation = _emailConfirmationOptions.Enabled,
                ConfirmationEmailSent = confirmationSent
            });
        }

        [HttpPost("email/confirm")]
        [EnableRateLimiting(RateLimitingPolicies.Auth)]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailDto dto)
        {
            if (!ModelState.IsValid)
            {
                throw new DomainValidationException("Email confirmation request is invalid.", CreateModelStateErrors());
            }

            if (!await _emailConfirmation.ConfirmAsync(dto.UserId, dto.Code))
            {
                throw new DomainValidationException("This confirmation link is invalid or has expired.");
            }

            return NoContent();
        }

        [HttpPost("email/resend")]
        [EnableRateLimiting(RateLimitingPolicies.EmailConfirmation)]
        public async Task<IActionResult> ResendEmailConfirmation([FromBody] ResendEmailConfirmationRequest? request)
        {
            if (MailAddress.TryCreate(request?.Email, out _))
            {
                await _emailConfirmation.ResendAsync(request!.Email!, HttpContext.RequestAborted);
            }

            return Accepted();
        }

        [HttpPost("login")]
        [EnableRateLimiting(RateLimitingPolicies.Auth)]
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

            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);

            if (!result.Succeeded || result.IsLockedOut)
            {
                throw new UnauthorizedAccessException("Invalid username or password.");
            }

            await _accountRestrictionService.EnsureCanLoginAsync(user);

            var tokenString = await _jwtTokenIssuer.IssueAsync(user);

            return Ok(new AuthResponseDto
            {
                IsSuccess = true,
                Token = tokenString
            });
        }

        [HttpPost("maintenance-bypass")]
        [Authorize(Roles = "Admin")]
        [EnableRateLimiting(RateLimitingPolicies.Write)]
        public IActionResult EnableMaintenanceBypass()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            _maintenanceBypassService.AppendCookie(Response, userId);
            return NoContent();
        }

        [HttpDelete("maintenance-bypass")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitingPolicies.Write)]
        public IActionResult DisableMaintenanceBypass()
        {
            _maintenanceBypassService.ClearCookie(Response);
            return NoContent();
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

    public sealed class ResendEmailConfirmationRequest
    {
        public string? Email { get; set; }
    }
}
