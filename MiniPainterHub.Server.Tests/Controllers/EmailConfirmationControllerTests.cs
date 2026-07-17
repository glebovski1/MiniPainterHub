using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Common.Auth;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Tests.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public sealed class EmailConfirmationControllerTests
{
    [Fact]
    public async Task RegistrationAndConfirmation_RequireVerifiedEmailBeforePasswordLogin()
    {
        using var factory = CreateFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var registration = await RegisterAsync(client, "new-painter", "new-painter@example.test", "/support");

        registration.IsSuccess.Should().BeTrue();
        registration.RequiresEmailConfirmation.Should().BeTrue();
        registration.ConfirmationEmailSent.Should().BeTrue();
        factory.EmailSender.Messages.Should().ContainSingle();
        factory.EmailSender.Messages.Single().ConfirmationLink.Should().Contain("returnUrl=%2Fsupport");

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByNameAsync("new-painter");
            user.Should().NotBeNull();
            user!.EmailConfirmed.Should().BeFalse();
        }

        var blockedLogin = await LoginAsync(client, "new-painter");
        blockedLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var confirmation = ParseConfirmation(factory.EmailSender.Messages.Single().ConfirmationLink);
        var confirmResponse = await client.PostAsJsonAsync("/api/auth/email/confirm", confirmation);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var repeatedConfirmation = await client.PostAsJsonAsync("/api/auth/email/confirm", confirmation);
        repeatedConfirmation.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var successfulLogin = await LoginAsync(client, "new-painter");
        successfulLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        (await successfulLogin.Content.ReadFromJsonAsync<AuthResponseDto>())!.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ConfirmEmail_WhenCodeIsTampered_ReturnsValidationProblem()
    {
        using var factory = CreateFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        await RegisterAsync(client, "tampered", "tampered@example.test");
        var confirmation = ParseConfirmation(factory.EmailSender.Messages.Single().ConfirmationLink);
        confirmation.Code += "tampered";

        var response = await client.PostAsJsonAsync("/api/auth/email/confirm", confirmation);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConfirmEmail_WhenCodeIsExpired_ReturnsValidationProblem()
    {
        using var factory = CreateFactory(TimeSpan.FromMilliseconds(10));
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        await RegisterAsync(client, "expired", "expired@example.test");
        var confirmation = ParseConfirmation(factory.EmailSender.Messages.Single().ConfirmationLink);
        await Task.Delay(100);

        var response = await client.PostAsJsonAsync("/api/auth/email/confirm", confirmation);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WhenEmailProviderFails_KeepsPendingAccountAndReturnsRecoveryState()
    {
        using var factory = CreateFactory();
        factory.EmailSender.FailSends = true;
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var registration = await RegisterAsync(client, "pending", "pending@example.test");

        registration.IsSuccess.Should().BeTrue();
        registration.RequiresEmailConfirmation.Should().BeTrue();
        registration.ConfirmationEmailSent.Should().BeFalse();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByNameAsync("pending");
        user.Should().NotBeNull();
        user!.EmailConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task Resend_ReturnsSameAcceptedResponseWithoutDisclosingAccountState()
    {
        using var factory = CreateFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        await RegisterAsync(client, "resend-user", "resend@example.test");
        factory.EmailSender.Messages.Should().HaveCount(1);

        var unknown = await client.PostAsJsonAsync(
            "/api/auth/email/resend",
            new ResendEmailConfirmationDto { Email = "unknown@example.test" });
        var existing = await client.PostAsJsonAsync(
            "/api/auth/email/resend",
            new ResendEmailConfirmationDto { Email = "resend@example.test" });
        var malformed = await client.PostAsJsonAsync(
            "/api/auth/email/resend",
            new ResendEmailConfirmationDto { Email = "not-an-email" });

        unknown.StatusCode.Should().Be(HttpStatusCode.Accepted);
        existing.StatusCode.Should().Be(HttpStatusCode.Accepted);
        malformed.StatusCode.Should().Be(HttpStatusCode.Accepted);
        factory.EmailSender.Messages.Should().HaveCount(2);
    }

    [Fact]
    public async Task RegistrationEmailPolicy_AllowsFiveRequestsPerHourPerIp()
    {
        using var factory = CreateFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var statuses = new List<HttpStatusCode>();

        for (var index = 0; index < 6; index++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterDto
            {
                UserName = $"limited-{index}",
                Email = $"limited-{index}@example.test",
                Password = "ValidPass123!"
            });
            statuses.Add(response.StatusCode);
        }

        statuses.Take(5).Should().OnlyContain(status => status == HttpStatusCode.OK);
        statuses.Last().Should().Be(HttpStatusCode.TooManyRequests);
    }

    private static IntegrationTestApplicationFactory CreateFactory(TimeSpan? tokenLifespan = null) => new(
        configurationOverrides: new Dictionary<string, string?>
        {
            ["EmailConfirmation:Enabled"] = "true",
            ["EmailConfirmation:Provider"] = "DevelopmentLog",
            ["EmailConfirmation:PublicOrigin"] = "https://localhost:7295"
        },
        emailConfirmationTokenLifespan: tokenLifespan);

    private static async Task<RegistrationResultDto> RegisterAsync(
        HttpClient client,
        string userName,
        string email,
        string? returnUrl = null)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterDto
        {
            UserName = userName,
            Email = email,
            Password = "ValidPass123!",
            ReturnUrl = returnUrl
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<RegistrationResultDto>())!;
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string userName) =>
        client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            UserName = userName,
            Password = "ValidPass123!"
        });

    private static ConfirmEmailDto ParseConfirmation(string confirmationLink)
    {
        var query = QueryHelpers.ParseQuery(new Uri(confirmationLink).Query);
        return new ConfirmEmailDto
        {
            UserId = query["userId"].ToString(),
            Code = query["code"].ToString()
        };
    }
}
