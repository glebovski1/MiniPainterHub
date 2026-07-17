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

public sealed class FakeGoogleAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly GoogleAuthenticationOptions _google;

    public FakeGoogleAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<GoogleAuthenticationOptions> google)
        : base(options, logger, encoder)
    {
        _google = google.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        properties.Items.TryGetValue("fakeScenario", out var scenario);
        if (string.Equals(scenario, "cancel", StringComparison.OrdinalIgnoreCase))
        {
            Response.Redirect("/api/auth/google/complete?error=cancelled");
            return;
        }

        if (string.Equals(scenario, "expired", StringComparison.OrdinalIgnoreCase))
        {
            Response.Redirect("/auth/external/callback?error=expired&provider=Google");
            return;
        }

        properties.Items.TryGetValue("fakeSubject", out var subject);
        properties.Items.TryGetValue("fakeEmail", out var email);
        properties.Items.TryGetValue("fakeName", out var displayName);
        if (string.Equals(scenario, "conflict", StringComparison.OrdinalIgnoreCase))
        {
            subject = string.IsNullOrWhiteSpace(subject) ? "google-conflict-subject" : subject;
            email = string.IsNullOrWhiteSpace(email) ? "user@local" : email;
            displayName = string.IsNullOrWhiteSpace(displayName) ? "Existing User" : displayName;
        }
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, string.IsNullOrWhiteSpace(subject) ? _google.FakeSubject : subject),
            new(ClaimTypes.Email, string.IsNullOrWhiteSpace(email) ? _google.FakeEmail : email),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(displayName) ? _google.FakeDisplayName : displayName),
            new("urn:google:email_verified", "true")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        await Context.SignInAsync(
            ExternalAuthenticationSchemes.ExternalCookie,
            principal,
            properties);
        Response.Redirect(properties.RedirectUri ?? "/api/auth/google/complete");
    }
}
