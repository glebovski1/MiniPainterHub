using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Controllers;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services;

public sealed class EmailConfirmationService : IEmailConfirmationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAccountEmailSender _sender;
    private readonly EmailConfirmationOptions _options;
    private readonly ILogger<EmailConfirmationService> _logger;

    public EmailConfirmationService(
        UserManager<ApplicationUser> userManager,
        IAccountEmailSender sender,
        IOptions<EmailConfirmationOptions> options,
        ILogger<EmailConfirmationService> logger)
    {
        _userManager = userManager;
        _sender = sender;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> SendConfirmationAsync(
        ApplicationUser user,
        string? returnUrl,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(user.Email))
        {
            return false;
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var safeReturnUrl = ExternalAuthenticationController.NormalizeReturnUrl(returnUrl);
        var confirmationLink = BuildConfirmationLink(user.Id, encodedToken, safeReturnUrl);

        try
        {
            await _sender.SendConfirmationAsync(user.Email, confirmationLink, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Email confirmation delivery failed.");
            return false;
        }
    }

    public async Task<bool> ConfirmAsync(string userId, string encodedCode)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return false;
        }

        if (await _userManager.IsEmailConfirmedAsync(user))
        {
            return true;
        }

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedCode));
        }
        catch (FormatException)
        {
            return false;
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded;
    }

    public async Task ResendAsync(string email, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user is null || await _userManager.IsEmailConfirmedAsync(user))
        {
            return;
        }

        await SendConfirmationAsync(user, returnUrl: "/", cancellationToken);
    }

    private string BuildConfirmationLink(string userId, string encodedToken, string returnUrl)
    {
        var origin = _options.PublicOrigin!.TrimEnd('/');
        return $"{origin}/confirm-email?userId={Uri.EscapeDataString(userId)}&code={Uri.EscapeDataString(encodedToken)}&returnUrl={Uri.EscapeDataString(returnUrl)}";
    }
}
