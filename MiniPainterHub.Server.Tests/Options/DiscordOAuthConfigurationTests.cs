using FluentAssertions;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Tests.Infrastructure;
using System.Collections.Generic;
using Xunit;

namespace MiniPainterHub.Server.Tests.Options;

public sealed class DiscordOAuthConfigurationTests
{
    [Fact]
    public void EnabledProvider_UsesExpectedEndpointsScopesAndTransientTokens()
    {
        using var factory = new IntegrationTestApplicationFactory(new Dictionary<string, string?>
        {
            ["Authentication:Discord:Enabled"] = "true",
            ["Authentication:Discord:ClientId"] = "discord-client-id",
            ["Authentication:Discord:ClientSecret"] = "discord-client-secret",
            ["Authentication:Discord:CallbackPath"] = "/signin-discord",
            ["Authentication:Discord:PublicOrigin"] = "https://public.example.test",
            ["Authentication:Discord:UseFakeProvider"] = "false",
            ["Site:SupportEmail"] = "support@example.test"
        });
        using var scope = factory.Services.CreateScope();

        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<OAuthOptions>>()
            .Get(ExternalAuthenticationSchemes.Discord);

        options.AuthorizationEndpoint.Should().Be("https://discord.com/oauth2/authorize");
        options.TokenEndpoint.Should().Be("https://discord.com/api/oauth2/token");
        options.UserInformationEndpoint.Should().Be("https://discord.com/api/v10/users/@me");
        options.CallbackPath.Value.Should().Be("/signin-discord");
        options.SignInScheme.Should().Be(ExternalAuthenticationSchemes.ExternalCookie);
        options.SaveTokens.Should().BeFalse();
        options.Scope.Should().BeEquivalentTo("identify", "email");
        options.Events.OnCreatingTicket.Should().NotBeNull();
    }
}
