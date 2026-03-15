using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public class ProfilesControllerTests
{
    [Fact]
    public async Task GetMyProfile_WhenUnauthenticated_ReturnsUnauthorized()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/profiles/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyProfile_WhenProfileMissing_ReturnsNotFoundProblemDetails()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.SeedUserAsync("profile-user", "profile-user");
        using var client = factory.CreateAuthenticatedClient("profile-user", "profile-user");

        var response = await client.GetAsync("/api/profiles/me");

        await ProblemDetailsAssertions.AssertAsync(
            response,
            HttpStatusCode.NotFound,
            "Not found",
            "Profile not found.");
    }

    [Fact]
    public async Task CreateMyProfile_WhenAuthenticated_CreatesProfile()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.SeedUserAsync("profile-user", "profile-user");
        using var client = factory.CreateAuthenticatedClient("profile-user", "profile-user");

        var response = await client.PostAsJsonAsync("/api/profiles/me", new CreateUserProfileDto
        {
            DisplayName = "Painter",
            Bio = "Bio"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        body.Should().NotBeNull();
        body!.UserId.Should().Be("profile-user");
        body.DisplayName.Should().Be("Painter");
    }

    [Fact]
    public async Task UpdateMyProfile_WhenAuthenticated_UpdatesProfile()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.SeedUserAsync("profile-user", "profile-user");
        await factory.SeedProfileAsync("profile-user", "Old", "Old bio");
        using var client = factory.CreateAuthenticatedClient("profile-user", "profile-user");

        var response = await client.PutAsJsonAsync("/api/profiles/me", new UpdateUserProfileDto
        {
            DisplayName = "Updated Name",
            Bio = "Updated bio"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        body.Should().NotBeNull();
        body!.DisplayName.Should().Be("Updated Name");
        body.Bio.Should().Be("Updated bio");
    }

    [Fact]
    public async Task GetMyProfile_WhenProfileExists_ReturnsProfile()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.SeedUserAsync("profile-user", "profile-user");
        await factory.SeedProfileAsync("profile-user", "Visible Name", "Visible bio");
        using var client = factory.CreateAuthenticatedClient("profile-user", "profile-user");

        var response = await client.GetAsync("/api/profiles/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        body.Should().NotBeNull();
        body!.DisplayName.Should().Be("Visible Name");
        body.Bio.Should().Be("Visible bio");
        body.Email.Should().Be("profile-user@example.test");
    }

    [Fact]
    public async Task GetUserProfileById_WhenProfileExists_ReturnsPublicProfileWithoutEmail()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.SeedUserAsync("target-user", "target-user");
        await factory.SeedProfileAsync("target-user", "Target Name", "Target bio");
        using var client = factory.CreateAuthenticatedClient("caller-user", "caller-user");

        var response = await client.GetAsync("/api/profiles/target-user");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var body = JsonSerializer.Deserialize<PublicUserProfileDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        body.Should().NotBeNull();
        body!.UserId.Should().Be("target-user");
        body.DisplayName.Should().Be("Target Name");
        document.RootElement.TryGetProperty("email", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GetUserProfileById_WhenUnauthenticated_ReturnsPublicProfileWithoutEmail()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.SeedUserAsync("target-user", "target-user");
        await factory.SeedProfileAsync("target-user", "Target Name", "Target bio");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/profiles/target-user");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var body = JsonSerializer.Deserialize<PublicUserProfileDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        body.Should().NotBeNull();
        body!.UserId.Should().Be("target-user");
        document.RootElement.TryGetProperty("email", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GetUserProfileById_WhenProfileMissing_ReturnsNotFoundProblemDetails()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient("caller-user", "caller-user");

        var response = await client.GetAsync("/api/profiles/missing-user");

        await ProblemDetailsAssertions.AssertAsync(
            response,
            HttpStatusCode.NotFound,
            "Not found",
            "Profile not found.");
    }
}
