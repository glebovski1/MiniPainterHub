using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Common.Auth;
using MiniPainterHub.Server.Controllers;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Tests.Infrastructure;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public sealed class ExternalAuthenticationControllerTests
{
    [Fact]
    public async Task Providers_WhenGoogleDisabled_ReturnsPublicConfiguration()
    {
        using var factory = new IntegrationTestApplicationFactory(new Dictionary<string, string?>
        {
            ["Site:SupportEmail"] = "support@example.test"
        });
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<AuthProvidersDto>("/api/auth/providers");

        result.Should().NotBeNull();
        result!.Google.Enabled.Should().BeFalse();
        result.Google.Name.Should().Be("Google");
        result.SupportEmail.Should().Be("support@example.test");
    }

    [Fact]
    public async Task StartGoogle_WhenProviderDisabled_ReturnsNotFound()
    {
        using var factory = new IntegrationTestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/google/start");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(null, "/")]
    [InlineData("/support?view=open", "/support?view=open")]
    [InlineData("//evil.example/path", "/")]
    [InlineData("https://evil.example/path", "/")]
    [InlineData("/\\evil", "/")]
    public void NormalizeReturnUrl_AllowsOnlyLocalApplicationPaths(string? value, string expected)
    {
        ExternalAuthenticationController.NormalizeReturnUrl(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Test", false)]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    public void RequiresSecureCookies_IsMandatoryOutsideDevelopmentAndTest(string environment, bool expected)
    {
        ExternalAuthenticationController.RequiresSecureCookies(environment).Should().Be(expected);
    }

    [Fact]
    public async Task FakeGoogle_FullRegistrationAndReturningLoginFlow_WorksWithoutProviderCredentials()
    {
        using var factory = CreateGoogleFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://127.0.0.1"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        await CompleteFakeChallengeAsync(client);
        var exchange = await (await client.PostAsync("/api/auth/external/exchange", null))
            .Content.ReadFromJsonAsync<ExternalAuthExchangeResponseDto>();
        exchange!.Outcome.Should().Be(ExternalAuthOutcomes.RegistrationRequired);

        var registrationResponse = await client.PostAsJsonAsync(
            "/api/auth/external/register",
            new ExternalAuthRegistrationDto { UserName = "fakegoogle" });
        registrationResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var registration = await registrationResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        registration!.Token.Should().NotBeNullOrWhiteSpace();

        await CompleteFakeChallengeAsync(client);
        var returningResponse = await client.PostAsync("/api/auth/external/exchange", null);
        returningResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var returning = await returningResponse.Content.ReadFromJsonAsync<ExternalAuthExchangeResponseDto>();
        returning!.Outcome.Should().Be(ExternalAuthOutcomes.Authenticated);
        returning.Token.Should().NotBeNullOrWhiteSpace();

        var replay = await client.PostAsync("/api/auth/external/exchange", null);
        replay.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task FakeGoogle_CancellationRedirectsToTypedCallbackError()
    {
        using var factory = CreateGoogleFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://127.0.0.1"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var start = await client.GetAsync("/api/auth/google/start?fake=cancel");
        start.StatusCode.Should().Be(HttpStatusCode.Redirect);
        start.Headers.Location!.OriginalString.Should().Be("/api/auth/google/complete?error=cancelled");
        var complete = await client.GetAsync(start.Headers.Location);
        complete.StatusCode.Should().Be(HttpStatusCode.Redirect);
        complete.Headers.Location!.OriginalString.Should().Be("/auth/external/callback?error=cancelled");
    }

    [Fact]
    public async Task FakeGoogle_ExpiredScenarioRedirectsWithoutIssuingProviderCookie()
    {
        using var factory = CreateGoogleFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://127.0.0.1"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync("/api/auth/google/start?fake=expired");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/auth/external/callback?error=expired");
    }

    [Fact]
    public async Task GoogleChallenge_UsesForwardedHttpsOriginForTheProviderCallback()
    {
        using var factory = new IntegrationTestApplicationFactory(
            new Dictionary<string, string?>
            {
                ["Authentication:Google:Enabled"] = "true",
                ["Authentication:Google:ClientId"] = "integration-client-id",
                ["Authentication:Google:ClientSecret"] = "integration-client-secret",
                ["Authentication:Google:CallbackPath"] = "/signin-google",
                ["Authentication:Google:PublicOrigin"] = "https://public.example.test",
                ["Authentication:Google:UseFakeProvider"] = "false",
                ["Site:SupportEmail"] = "support@example.test",
                ["ForwardedHeaders:Enabled"] = "true",
                ["ForwardedHeaders:TrustAllProxies"] = "true"
            },
            useTestAuthentication: false,
            registerRealGoogleHandler: true);
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://public.example.test"),
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-Proto", "https");

        var response = await client.GetAsync("/api/auth/google/start");
        var responseBody = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Redirect, responseBody);
        response.Headers.Location.Should().NotBeNull();
        Uri.UnescapeDataString(response.Headers.Location!.Query)
            .Should().Contain("redirect_uri=https://public.example.test/signin-google");
    }

    [Fact]
    public async Task FakeGoogle_ConflictNeverMerges_ThenAuthenticatedLinkCompletes()
    {
        using var factory = CreateGoogleFactory();
        await factory.ResetDatabaseAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var local = new ApplicationUser
            {
                Id = TestAuthHandler.DefaultUserId,
                UserName = TestAuthHandler.DefaultUserName,
                Email = "user@local"
            };
            (await users.CreateAsync(local, "ValidPass123!")).Succeeded.Should().BeTrue();
        }

        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://127.0.0.1"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var conflictStart = await client.GetAsync("/api/auth/google/start?fake=conflict");
        var conflictComplete = await client.GetAsync(conflictStart.Headers.Location);
        conflictComplete.Headers.Location!.OriginalString.Should().Be("/auth/external/callback");
        var conflictResponse = await client.PostAsync("/api/auth/external/exchange", null);
        var conflict = await conflictResponse.Content.ReadFromJsonAsync<ExternalAuthExchangeResponseDto>();
        conflict!.Outcome.Should().Be(ExternalAuthOutcomes.EmailConflict);

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TestAuthHandler.SchemeName);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, TestAuthHandler.DefaultUserId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserNameHeader, TestAuthHandler.DefaultUserName);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, TestAuthHandler.DefaultRole);
        var intent = await client.PostAsync("/api/auth/google/link-intent", null);
        intent.StatusCode.Should().Be(HttpStatusCode.OK);
        var linkStart = await client.GetAsync("/api/auth/google/start?fake=conflict");
        var linkComplete = await client.GetAsync(linkStart.Headers.Location);
        linkComplete.Headers.Location!.OriginalString.Should().Be("/auth/external/callback");
        var linkResponse = await client.PostAsync("/api/auth/external/exchange", null);
        var linked = await linkResponse.Content.ReadFromJsonAsync<ExternalAuthExchangeResponseDto>();
        linked!.Outcome.Should().Be(ExternalAuthOutcomes.LinkCompleted);

        using var verificationScope = factory.Services.CreateScope();
        var verificationUsers = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = await verificationUsers.FindByIdAsync(TestAuthHandler.DefaultUserId);
        (await verificationUsers.GetLoginsAsync(existing!)).Should().ContainSingle(login => login.ProviderKey == "google-conflict-subject");
    }

    private static IntegrationTestApplicationFactory CreateGoogleFactory() => new(new Dictionary<string, string?>
    {
        ["Authentication:Google:Enabled"] = "true",
        ["Authentication:Google:UseFakeProvider"] = "true",
        ["Authentication:Google:PublicOrigin"] = "https://localhost",
        ["Site:SupportEmail"] = "support@example.test"
    });

    private static async Task CompleteFakeChallengeAsync(HttpClient client)
    {
        var start = await client.GetAsync("/api/auth/google/start?returnUrl=%2Fsupport");
        start.StatusCode.Should().Be(HttpStatusCode.Redirect);
        start.Headers.Location!.OriginalString.Should().Be("/api/auth/google/complete");
        var complete = await client.GetAsync(start.Headers.Location);
        complete.StatusCode.Should().Be(HttpStatusCode.Redirect);
        complete.Headers.Location!.OriginalString.Should().Be("/auth/external/callback");
    }
}
