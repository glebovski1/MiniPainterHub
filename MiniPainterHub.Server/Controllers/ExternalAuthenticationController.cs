using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using MiniPainterHub.Common.Auth;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Infrastructure.RateLimiting;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class ExternalAuthenticationController : ControllerBase
{
    private const string ExchangeCookie = "mph.external-auth";
    private const string LinkIntentCookie = "mph.external-link";
    private const string PurposeItem = "mph.purpose";
    private const string TargetItem = "mph.target";
    private const string ReturnUrlItem = "mph.return-url";

    private readonly IExternalAuthenticationService _externalAuthentication;
    private readonly GoogleAuthenticationOptions _google;
    private readonly IConfiguration _configuration;
    private readonly ITimeLimitedDataProtector _linkIntentProtector;
    private readonly ILogger<ExternalAuthenticationController> _logger;
    private readonly bool _secureCookies;

    public ExternalAuthenticationController(
        IExternalAuthenticationService externalAuthentication,
        IOptions<GoogleAuthenticationOptions> google,
        IConfiguration configuration,
        IDataProtectionProvider dataProtectionProvider,
        IHostEnvironment environment,
        ILogger<ExternalAuthenticationController> logger)
    {
        _externalAuthentication = externalAuthentication;
        _google = google.Value;
        _configuration = configuration;
        _linkIntentProtector = dataProtectionProvider
            .CreateProtector("MiniPainterHub.ExternalAuth.LinkIntent.v1")
            .ToTimeLimitedDataProtector();
        _secureCookies = RequiresSecureCookies(environment.EnvironmentName);
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet("providers")]
    public ActionResult<AuthProvidersDto> GetProviders() => Ok(new AuthProvidersDto
    {
        Google = new AuthProviderDto
        {
            Name = "Google",
            DisplayName = "Google",
            Enabled = _google.Enabled
        },
        SupportEmail = EmptyToNull(_configuration["Site:SupportEmail"])
    });

    [AllowAnonymous]
    [HttpGet("google/start")]
    [EnableRateLimiting(RateLimitingPolicies.Auth)]
    public IActionResult StartGoogle(
        [FromQuery] string? returnUrl = null,
        [FromQuery] string? fake = null,
        [FromQuery] string? fakeSubject = null,
        [FromQuery] string? fakeEmail = null,
        [FromQuery] string? fakeName = null)
    {
        EnsureGoogleEnabled();
        var safeReturnUrl = NormalizeReturnUrl(returnUrl);
        var purpose = ExternalAuthPurposes.SignIn;
        string? targetUserId = null;

        if (Request.Cookies.TryGetValue(LinkIntentCookie, out var protectedIntent))
        {
            Response.Cookies.Delete(LinkIntentCookie, SecureCookieOptions(TimeSpan.Zero));
            try
            {
                var parts = _linkIntentProtector.Unprotect(protectedIntent).Split('\n', 2);
                if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
                {
                    throw new InvalidOperationException();
                }
                purpose = ExternalAuthPurposes.Link;
                targetUserId = parts[0];
                safeReturnUrl = NormalizeReturnUrl(parts[1]);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _logger.LogInformation("External authentication link intent rejected. Outcome={Outcome}.", "invalid_intent");
                throw new GoneException("The external authentication link request is no longer available.");
            }
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = "/api/auth/google/complete"
        };
        properties.Items[PurposeItem] = purpose;
        properties.Items[ReturnUrlItem] = safeReturnUrl;
        if (targetUserId is not null)
        {
            properties.Items[TargetItem] = targetUserId;
        }
        if (_google.UseFakeProvider && !string.IsNullOrWhiteSpace(fake))
        {
            properties.Items["fakeScenario"] = fake;
        }
        if (_google.UseFakeProvider)
        {
            AddFakeValue(properties, "fakeSubject", fakeSubject, 256);
            AddFakeValue(properties, "fakeEmail", fakeEmail, 256);
            AddFakeValue(properties, "fakeName", fakeName, 256);
        }

        _logger.LogInformation("External authentication challenge started. Provider={Provider}; Purpose={Purpose}; Outcome={Outcome}.", "Google", purpose, "challenge");
        return Challenge(properties, _google.UseFakeProvider
            ? ExternalAuthenticationSchemes.FakeGoogle
            : GoogleDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    [HttpGet("google/complete")]
    [EnableRateLimiting(RateLimitingPolicies.Auth)]
    public async Task<IActionResult> CompleteGoogle([FromQuery] string? error = null)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            await HttpContext.SignOutAsync(ExternalAuthenticationSchemes.ExternalCookie);
            _logger.LogInformation("External authentication callback completed. Provider={Provider}; Outcome={Outcome}.", "Google", "cancelled");
            return Redirect("/auth/external/callback?error=cancelled");
        }

        EnsureGoogleEnabled();
        var authentication = await HttpContext.AuthenticateAsync(ExternalAuthenticationSchemes.ExternalCookie);
        if (!authentication.Succeeded || authentication.Principal is null)
        {
            _logger.LogInformation("External authentication callback completed. Provider={Provider}; Outcome={Outcome}.", "Google", "invalid_callback");
            return Redirect("/auth/external/callback?error=invalid");
        }

        var subject = authentication.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = authentication.Principal.FindFirstValue(ClaimTypes.Email);
        var verified = authentication.Principal.FindFirstValue("urn:google:email_verified");
        if (string.IsNullOrWhiteSpace(subject)
            || string.IsNullOrWhiteSpace(email)
            || !string.Equals(verified, "true", StringComparison.OrdinalIgnoreCase))
        {
            await HttpContext.SignOutAsync(ExternalAuthenticationSchemes.ExternalCookie);
            _logger.LogInformation("External authentication callback completed. Provider={Provider}; Outcome={Outcome}.", "Google", "unverified_identity");
            return Redirect("/auth/external/callback?error=unverified");
        }

        var properties = authentication.Properties;
        var purpose = properties?.Items.TryGetValue(PurposeItem, out var purposeValue) == true
            ? purposeValue
            : ExternalAuthPurposes.SignIn;
        string? targetUserId = null;
        string? returnUrl = null;
        properties?.Items.TryGetValue(TargetItem, out targetUserId);
        properties?.Items.TryGetValue(ReturnUrlItem, out returnUrl);
        var rawHandle = await _externalAuthentication.CreateExchangeAsync(
            new ExternalIdentity("Google", subject, email, authentication.Principal.FindFirstValue(ClaimTypes.Name)),
            string.Equals(purpose, ExternalAuthPurposes.Link, StringComparison.Ordinal) ? ExternalAuthPurposes.Link : ExternalAuthPurposes.SignIn,
            targetUserId,
            NormalizeReturnUrl(returnUrl));

        Response.Cookies.Append(ExchangeCookie, rawHandle, SecureCookieOptions(TimeSpan.FromMinutes(10)));
        await HttpContext.SignOutAsync(ExternalAuthenticationSchemes.ExternalCookie);
        return Redirect("/auth/external/callback");
    }

    [AllowAnonymous]
    [HttpPost("external/exchange")]
    [EnableRateLimiting(RateLimitingPolicies.Auth)]
    public async Task<ActionResult<ExternalAuthExchangeResponseDto>> Exchange()
    {
        var result = await _externalAuthentication.ExchangeAsync(ReadExchangeCookie());
        if (!string.Equals(result.Outcome, ExternalAuthOutcomes.RegistrationRequired, StringComparison.Ordinal))
        {
            ClearExchangeCookie();
        }
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("external/register")]
    [EnableRateLimiting(RateLimitingPolicies.Auth)]
    public async Task<ActionResult<AuthResponseDto>> RegisterExternal([FromBody] ExternalAuthRegistrationDto request)
    {
        var result = await _externalAuthentication.RegisterAsync(ReadExchangeCookie(), request);
        ClearExchangeCookie();
        return Ok(result);
    }

    [Authorize]
    [HttpPost("google/link-intent")]
    [EnableRateLimiting(RateLimitingPolicies.Auth)]
    public ActionResult<ExternalAuthStartDto> CreateLinkIntent([FromQuery] string? returnUrl = null)
    {
        EnsureGoogleEnabled();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var safeReturnUrl = NormalizeReturnUrl(returnUrl ?? "/account/sign-in-methods");
        var protectedIntent = _linkIntentProtector.Protect(userId + "\n" + safeReturnUrl, TimeSpan.FromMinutes(10));
        Response.Cookies.Append(LinkIntentCookie, protectedIntent, SecureCookieOptions(TimeSpan.FromMinutes(10)));
        return Ok(new ExternalAuthStartDto
        {
            StartUrl = "/api/auth/google/start?returnUrl=" + Uri.EscapeDataString(safeReturnUrl)
        });
    }

    [Authorize]
    [HttpGet("sign-in-methods")]
    public async Task<ActionResult<SignInMethodsDto>> GetSignInMethods() =>
        Ok(await _externalAuthentication.GetSignInMethodsAsync(RequireUserId()));

    [Authorize]
    [HttpPost("password/set")]
    [EnableRateLimiting(RateLimitingPolicies.Auth)]
    public async Task<ActionResult<SignInMethodsDto>> SetPassword([FromBody] SetPasswordDto request) =>
        Ok(await _externalAuthentication.SetPasswordAsync(RequireUserId(), request));

    [Authorize]
    [HttpDelete("google")]
    [EnableRateLimiting(RateLimitingPolicies.Auth)]
    public async Task<ActionResult<SignInMethodsDto>> DisconnectGoogle() =>
        Ok(await _externalAuthentication.DisconnectGoogleAsync(RequireUserId()));

    internal static string NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        var value = returnUrl.Trim();
        if (!value.StartsWith("/", StringComparison.Ordinal)
            || value.StartsWith("//", StringComparison.Ordinal)
            || value.Contains("\\", StringComparison.Ordinal))
        {
            return "/";
        }
        return value.Length <= 2048 ? value : "/";
    }

    private void EnsureGoogleEnabled()
    {
        if (!_google.Enabled)
        {
            throw new NotFoundException("Google authentication is not available.");
        }
    }

    private string RequireUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException("User is not authenticated.");

    private string ReadExchangeCookie() =>
        Request.Cookies.TryGetValue(ExchangeCookie, out var handle) ? handle : string.Empty;

    private void ClearExchangeCookie() =>
        Response.Cookies.Delete(ExchangeCookie, SecureCookieOptions(TimeSpan.Zero));

    private CookieOptions SecureCookieOptions(TimeSpan lifetime) => new()
    {
        HttpOnly = true,
        Secure = _secureCookies,
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
        Path = "/api/auth",
        MaxAge = lifetime
    };

    internal static bool RequiresSecureCookies(string environmentName) =>
        !string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(environmentName, "Test", StringComparison.OrdinalIgnoreCase);

    private static void AddFakeValue(AuthenticationProperties properties, string key, string? value, int maxLength)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            properties.Items[key] = value.Trim().Length <= maxLength ? value.Trim() : value.Trim()[..maxLength];
        }
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
