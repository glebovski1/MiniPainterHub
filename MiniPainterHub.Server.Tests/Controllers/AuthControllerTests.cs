using System.Linq;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Common.Auth;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public class AuthControllerTests
{
    [Fact]
    public async Task Register_WhenRequestIsValid_ReturnsSuccessAndCreatesProfile()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterDto
        {
            UserName = "newuser",
            Email = "newuser@example.test",
            Password = "ValidPass123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        body.Should().NotBeNull();
        body!.IsSuccess.Should().BeTrue();
        body.Token.Should().BeNull();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.Count().Should().Be(1);
        db.Profiles.Count().Should().Be(1);
        db.Profiles.Single().DisplayName.Should().Be("newuser");
    }

    [Fact]
    public async Task Login_WhenCredentialsAreValid_ReturnsJwtToken()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", new RegisterDto
        {
            UserName = "loginuser",
            Email = "loginuser@example.test",
            Password = "ValidPass123!"
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            UserName = "loginuser",
            Password = "ValidPass123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        body.Should().NotBeNull();
        body!.IsSuccess.Should().BeTrue();
        body.Token.Should().NotBeNullOrWhiteSpace();

        var token = new JwtSecurityTokenHandler().ReadJwtToken(body.Token);
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value.Length > 0);
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.UniqueName && c.Value == "loginuser");
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti && c.Value.Length > 0);
    }

    [Fact]
    public async Task Login_WhenCredentialsAreInvalid_ReturnsUnauthorizedProblemDetails()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", new RegisterDto
        {
            UserName = "loginuser",
            Email = "loginuser@example.test",
            Password = "ValidPass123!"
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            UserName = "loginuser",
            Password = "WrongPass123!"
        });

        await ProblemDetailsAssertions.AssertAsync(
            response,
            HttpStatusCode.Unauthorized,
            "Unauthorized",
            "Invalid username or password.");
    }

    [Fact]
    public async Task Login_WhenSuspendedUserPasswordIsInvalid_ReturnsUnauthorizedProblemDetails()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", new RegisterDto
        {
            UserName = "suspended-user",
            Email = "suspended-user@example.test",
            Password = "ValidPass123!"
        });

        await factory.ExecuteDbContextAsync(async db =>
        {
            var user = db.Users.Single(u => u.UserName == "suspended-user");
            user.SuspendedUntilUtc = DateTime.UtcNow.AddHours(1);
            user.SuspensionReason = "policy";
            user.SuspensionUpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            UserName = "suspended-user",
            Password = "WrongPass123!"
        });

        await ProblemDetailsAssertions.AssertAsync(
            response,
            HttpStatusCode.Unauthorized,
            "Unauthorized",
            "Invalid username or password.");
    }

    [Fact]
    public async Task Login_WhenSuspendedUserPasswordIsValid_ReturnsForbiddenProblemDetails()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", new RegisterDto
        {
            UserName = "suspended-user",
            Email = "suspended-user@example.test",
            Password = "ValidPass123!"
        });

        await factory.ExecuteDbContextAsync(async db =>
        {
            var user = db.Users.Single(u => u.UserName == "suspended-user");
            user.SuspendedUntilUtc = DateTime.UtcNow.AddHours(1);
            user.SuspensionReason = "policy";
            user.SuspensionUpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            UserName = "suspended-user",
            Password = "ValidPass123!"
        });

        await ProblemDetailsAssertions.AssertAsync(
            response,
            HttpStatusCode.Forbidden,
            "Forbidden",
            "Your account is suspended.");
    }

    [Fact]
    public async Task Register_WhenModelStateInvalid_ReturnsBadRequestProblemDetailsWithErrors()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/auth/register", content);

        await ProblemDetailsAssertions.AssertAsync(
            response,
            HttpStatusCode.BadRequest,
            "One or more validation errors occurred.",
            expectedErrorKeys: new[] { "UserName", "Email", "Password" });
    }

    [Fact]
    public async Task Login_WhenModelStateInvalid_ReturnsBadRequestProblemDetailsWithErrors()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/auth/login", content);

        await ProblemDetailsAssertions.AssertAsync(
            response,
            HttpStatusCode.BadRequest,
            "One or more validation errors occurred.",
            expectedErrorKeys: new[] { "UserName", "Password" });
    }

    [Fact]
    public async Task Register_WhenUserAlreadyExists_ReturnsBadRequestProblemDetailsWithIdentityErrors()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var dto = new RegisterDto
        {
            UserName = "duplicate-user",
            Email = "duplicate-user@example.test",
            Password = "ValidPass123!"
        };

        await client.PostAsJsonAsync("/api/auth/register", dto);
        var duplicate = await client.PostAsJsonAsync("/api/auth/register", dto);

        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var payload = await duplicate.Content.ReadAsStringAsync();
        payload.Should().Contain("DuplicateUserName");
    }
}
