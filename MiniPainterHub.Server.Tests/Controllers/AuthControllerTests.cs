using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("title").GetString().Should().Be("Unauthorized");
        json.RootElement.GetProperty("detail").GetString().Should().Be("Invalid username or password.");
    }

    [Fact]
    public async Task Register_WhenModelStateInvalid_ReturnsBadRequestProblemDetailsWithErrors()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/auth/register", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("status").GetInt32().Should().Be(400);
        json.RootElement.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.EnumerateObject().Select(e => e.Name)
            .Should().Contain(new[] { "UserName", "Email", "Password" });
    }
}
