using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private const string ProviderItem = "mph.provider";
    private const string PurposeItem = "mph.purpose";
    private const string TargetItem = "mph.target";
    private const string ReturnUrlItem = "mph.return-url";

    private readonly IExternalAuthenticationService _externalAuthentication;
    private readonly GoogleAuthenticationOptions _google;
    private readonly DiscordAuthenticationOptions _discord;
    private readonly IConfiguration _configuration;
    private readonly ITimeLimitedDataProtector _linkIntentProtector;
    private readonly ILogger<ExternalAuthenticationController> _logger;
    private readonly bool _secureCookies;

    public ExternalAuthenticationController(
        IExternalAuthenticationService externalAuthentication,
        IOptions<GoogleAuthenticationOptions> google,
        IOptions<DiscordAuthenticationOptions> discord,
        IConfiguration configuration,
        IDataProtectionProvider dataProtectionProvider,
        IHostEnvironment environment,
        ILogger<ExternalAuthenticationController> logger)
    {
        _externalAuthentication = externalAuthentication;
        _google = google.Value;
        _discord = discord.Value;
        _configuration = configuration;
        _linkIntentProtector = dataProtectionProvider
            .CreateProtector("MiniPainterHub.ExternalAuth.LinkIntent.v2")
            .ToTimeLimitedDataProtector();
        _secureCookies = RequiresSecureCookies(environment.EnvironmentName);
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet("providers")]
    public ActionResult<AuthProvidersDto> GetProviders() => Ok(new AuthProvidersDto
    {
        Google = CreateProviderDto(ExternalAuthProviderNames.Google, _google.Enabled),
        Discord = CreateProviderDto(ExternalAuthProviderNames.Discord, _discord.Enabled),
        SupportEmail = EmptyToNull(_configuration["Site:SupportEmail"])
    });

    [AllowAnonymous]
    [HttpGet("{provider}/start")]
    [EnableRateLimiting(RateLimitingPolicies.Auth)]
    public IActionResult StartExternal(
        [FromRoute] string provider,
        [FromQuery] string? returnUrl = null,
        [FromQuery] string? fake = null,
        [FromQuery] string? fakeSubject = null,
        [FromQuery] string? fakeEmail = null,
        [FromQuery] string? fakeName = null)
    {
        var descriptor = RequireEnabledProvider(provider);
        var safeReturnUrl = NormalizeReturnUrl(returnUrl);
        var purpose = ExternalAuthPurposes.SignIn;
        string? targetUserId = null;

        if (Request.Cookies.TryGetValue(LinkIntentCookie, out var protectedIntent))
        {
            Response.Cookies.Delete(LinkIntentCookie, SecureCookieOptions(TimeSpan.Zero));
            try
            {
                var parts = _linkIntentProtector.Unprotect(protectedIntent).Split('\n', 3);
                if (parts.Length != 3
                    || !string.Equals(parts[0], descriptor.Name, StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(parts[1]))
                {
                    throw new InvalidOperationException();
                }

                purpose = ExternalAuthPurposes.Link;
                targetUserId = parts[1];
                safeReturnUrl = NormalizeReturnUrl(parts[2]);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _logger.LogInformation(
                    "External authentication link intent rejected. Provider={Provider}; Outcome={Outcome}.",
                    descriptor.Name,
                    "invalid_intent");
                throw new GoneException("The external authentication link request is no longer available.");
            }
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = $"/api/auth/{descriptor.Slug}/complete"
        };
        properties.Items[ProviderItem] = descriptor.Name;
        properties.Items[PurposeItem] = purpose;
        properties.Items[ReturnUrlItem] = safeReturnUrl;
        if (targetUserId is not null)
        {
            properties.Items[TargetItem] = targetUserId;
        }
        if (descriptor.UseFakeProvider && !string.IsNullOrWhiteSpace(fake))
        {
            properties.Items["fakeScenario"] = fake;
        }
        if (descriptor.UseFakeProvider)
        {
            AddFakeValue(properties, "fakeSubject", fakeSubject, 256);
            AddFakeValue(properties, "fakeEmail", fakeEmail, 256);
            AddFakeValue(properties, "fakeName", fakeName, 256);
        }

        _logger.LogInformation(
            "External authentication challenge started. Provider={Provider}; Purpose={Purpose}; Outcome={Outcome}.",
            descriptor.Name,
            purpose,
            "challenge");
        return Challenge(properties, descriptor.AuthenticationScheme);
    }

    [AllowAnonymous]
    [HttpGet("{provider}/complete")]
    [EnableRateLimiting(RateLimitingPolicies.Auth)]
    public async Task<IActionResult> CompleteExternal([FromRoute] string provider, [FromQuery] string? error = null)
    {
        var descriptor = RequireEnabledProvider(provider);
        if (!string.IsNullOrWhiteSpace(error))
        {
            await HttpContext.SignOutAsync(ExternalAuthenticationSchemes.ExternalCookie);
            _logger.LogInformation(
                "External authentication callback completed. Provider={Provider}; Outcome={Outcome}.",
                descriptor.Name,
                "cancelled");
            return Redirect(ClientCallback(descriptor.Name, "cancelled"));
        }

        var authentication = await HttpContext.AuthenticateAsync(ExternalAuthenticationSchemes.ExternalCookie);
        if (!authentication.Succeeded || authentication.Principal is null)
        {
            _logger.LogInformation(
                "External authentication callback completed. Provider={Provider}; Outcome={Outcome}.",
                descriptor.Name,
                "invalid_callback");
            return Redirect(ClientCallback(descriptor.Name, "invalid"));
        }

        var properties = authentication.Properties;
        string? protectedProvider = null;
        properties?.Items.TryGetValue(ProviderItem, out protectedProvider);
        if (!string.Equals(protectedProvider, descriptor.Name, StringComparison.Ordinal))
        {
            await HttpContext.SignOutAsync(ExternalAuthenticationSchemes.ExternalCookie);
            _logger.LogInformation(
                "External authentication callback completed. Provider={Provider}; Outcome={Outcome}.",
                descriptor.Name,
                "provider_mismatch");
            return Redirect(ClientCallback(descriptor.Name, "invalid"));
        }

        var subject = authentication.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = authentication.Principal.FindFirstValue(ClaimTypes.Email);
        var verified = authentication.Principal.FindFirstValue(descriptor.VerifiedEmailClaim);
        var rejectedDiscordIdentity = string.Equals(descriptor.Name, ExternalAuthProviderNames.Discord, StringComparison.Ordinal)
            && (IsTrue(authentication.Principal.FindFirstValue("urn:discord:bot"))
                || IsTrue(authentication.Principal.FindFirstValue("urn:discord:system")));
        if (string.IsNullOrWhiteSpace(subject)
            || string.IsNullOrWhiteSpace(email)
            || !IsTrue(verified)
            || rejectedDiscordIdentity)
        {
            await HttpContext.SignOutAsync(ExternalAuthenticationSchemes.ExternalCookie);
            _logger.LogInformation(
                "External authentication callback completed. Provider={Provider}; Outcome={Outcome}.",
                descriptor.Name,
                "unverified_identity");
            return Redirect(ClientCallback(descriptor.Name, "unverified"));
        }

        var purpose = properties?.Items.TryGetValue(PurposeItem, out var purposeValue) == true
            ? purposeValue
            : ExternalAuthPurposes.SignIn;
        string? targetUserId = null;
        string? returnUrl = null;
        properties?.Items.TryGetValue(TargetItem, out targetUserId);
        properties?.Items.TryGetValue(ReturnUrlItem, out returnUrl);
        var displayName = authentication.Principal.FindFirstValue(ClaimTypes.Name)
            ?? authentication.Principal.FindFirstValue("urn:discord:username");
        var rawHandle = await _externalAuthentication.CreateExchangeAsync(
            new ExternalIdentity(descriptor.Name, subject, email, displayName),
            string.Equals(purpose, ExternalAuthPurposes.Link, StringComparison.Ordinal)
                ? ExternalAuthPurposes.Link
                : ExternalAuthPurposes.SignIn,
            targetUserId,
            NormalizeReturnUrl(returnUrl));

        Response.Cookies.Append(ExchangeCookie, rawHandle, SecureCookieOptions(TimeSpan.FromMinutes(10)));
        await HttpContext.SignOutAsync(ExternalAuthenticationSchemes.ExternalCookie);
        return Redirect(ClientCallback(descriptor.Name));
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
    [HttpPost("{provider}/link-intent")]
    [EnableRateLimiting(RateLimitingPolicies.Auth)]
    public ActionResult<ExternalAuthStartDto> CreateLinkIntent([FromRoute] string provider, [FromQuery] string? returnUrl = null)
    {
        var descriptor = RequireEnabledProvider(provider);
        var userId = RequireUserId();
        var safeReturnUrl = NormalizeReturnUrl(returnUrl ?? "/account/sign-in-methods");
        var protectedIntent = _linkIntentProtector.Protect(
            descriptor.Name + "\n" + userId + "\n" + safeReturnUrl,
            TimeSpan.FromMinutes(10));
        Response.Cookies.Append(LinkIntentCookie, protectedIntent, SecureCookieOptions(TimeSpan.FromMinutes(10)));
        return Ok(new ExternalAuthStartDto
        {
            StartUrl = $"/api/auth/{descriptor.Slug}/start?returnUrl={Uri.EscapeDataString(safeReturnUrl)}"
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
    [HttpDelete("{provider}")]
    [EnableRateLimiting(RateLimitingPolicies.Auth)]
    public async Task<ActionResult<SignInMethodsDto>> DisconnectExternal([FromRoute] string provider)
    {
        var descriptor = RequireEnabledProvider(provider);
        return Ok(await _externalAuthentication.DisconnectAsync(RequireUserId(), descriptor.Name));
    }

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

    private ProviderDescriptor RequireEnabledProvider(string provider)
    {
        if (!ExternalAuthenticationProviders.TryResolve(provider, out var canonicalProvider, out var slug))
        {
            throw new NotFoundException("The external sign-in provider is not available.");
        }

        ProviderDescriptor descriptor;
        if (string.Equals(canonicalProvider, ExternalAuthProviderNames.Google, StringComparison.Ordinal))
        {
            descriptor = new ProviderDescriptor(
                canonicalProvider,
                slug,
                _google.Enabled,
                _google.UseFakeProvider,
                _google.UseFakeProvider ? ExternalAuthenticationSchemes.FakeGoogle : GoogleDefaults.AuthenticationScheme,
                "urn:google:email_verified");
        }
        else
        {
            descriptor = new ProviderDescriptor(
                canonicalProvider,
                slug,
                _discord.Enabled,
                _discord.UseFakeProvider,
                _discord.UseFakeProvider ? ExternalAuthenticationSchemes.FakeDiscord : ExternalAuthenticationSchemes.Discord,
                "urn:discord:email_verified");
        }

        if (!descriptor.Enabled)
        {
            throw new NotFoundException($"{descriptor.Name} authentication is not available.");
        }

        return descriptor;
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

    private static AuthProviderDto CreateProviderDto(string provider, bool enabled) => new()
    {
        Name = provider,
        DisplayName = provider,
        Enabled = enabled
    };

    private static bool IsTrue(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static string ClientCallback(string provider, string? error = null) =>
        "/auth/external/callback?provider=" + Uri.EscapeDataString(provider)
        + (string.IsNullOrWhiteSpace(error) ? string.Empty : "&error=" + Uri.EscapeDataString(error));

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ProviderDescriptor(
        string Name,
        string Slug,
        bool Enabled,
        bool UseFakeProvider,
        string AuthenticationScheme,
        string VerifiedEmailClaim);
}
