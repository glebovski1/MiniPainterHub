using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Options;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Identity;

public sealed class FakeDiscordAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly DiscordAuthenticationOptions _discord;

    public FakeDiscordAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<DiscordAuthenticationOptions> discord)
        : base(options, logger, encoder)
    {
        _discord = discord.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        properties.Items.TryGetValue("fakeScenario", out var scenario);
        if (string.Equals(scenario, "cancel", StringComparison.OrdinalIgnoreCase))
        {
            Response.Redirect("/api/auth/discord/complete?error=cancelled");
            return;
        }

        if (string.Equals(scenario, "expired", StringComparison.OrdinalIgnoreCase))
        {
            Response.Redirect("/auth/external/callback?error=expired&provider=Discord");
            return;
        }

        properties.Items.TryGetValue("fakeSubject", out var subject);
        properties.Items.TryGetValue("fakeEmail", out var email);
        properties.Items.TryGetValue("fakeName", out var displayName);
        if (string.Equals(scenario, "conflict", StringComparison.OrdinalIgnoreCase))
        {
            subject = string.IsNullOrWhiteSpace(subject) ? "discord-conflict-subject" : subject;
            email = string.IsNullOrWhiteSpace(email) ? "user@local" : email;
            displayName = string.IsNullOrWhiteSpace(displayName) ? "Existing User" : displayName;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, string.IsNullOrWhiteSpace(subject) ? _discord.FakeSubject : subject),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(displayName) ? _discord.FakeDisplayName : displayName),
            new("urn:discord:username", string.IsNullOrWhiteSpace(displayName) ? _discord.FakeDisplayName : displayName),
            new("urn:discord:email_verified", string.Equals(scenario, "unverified", StringComparison.OrdinalIgnoreCase) ? "false" : "true")
        };
        if (!string.Equals(scenario, "missing-email", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Email, string.IsNullOrWhiteSpace(email) ? _discord.FakeEmail : email));
        }
        if (string.Equals(scenario, "bot", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim("urn:discord:bot", "true"));
        }
        if (string.Equals(scenario, "system", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim("urn:discord:system", "true"));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        await Context.SignInAsync(ExternalAuthenticationSchemes.ExternalCookie, principal, properties);
        Response.Redirect(properties.RedirectUri ?? "/api/auth/discord/complete");
    }
}
