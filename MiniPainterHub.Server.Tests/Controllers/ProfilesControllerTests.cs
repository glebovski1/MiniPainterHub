using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Identity;
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
        await SeedUserAsync(factory, "profile-user", "profile-user");
        using var client = factory.CreateAuthenticatedClient("profile-user", "profile-user");

        var response = await client.GetAsync("/api/profiles/me");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("title").GetString().Should().Be("Not found");
        json.RootElement.GetProperty("detail").GetString().Should().Be("Profile not found.");
    }

    [Fact]
    public async Task CreateMyProfile_WhenAuthenticated_CreatesProfile()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "profile-user", "profile-user");
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
        await SeedUserAsync(factory, "profile-user", "profile-user");
        await factory.ExecuteDbContextAsync(async db =>
        {
            await db.Profiles.AddAsync(new Profile
            {
                UserId = "profile-user",
                DisplayName = "Old",
                Bio = "Old bio"
            });
            await db.SaveChangesAsync();
        });
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
        await SeedUserAsync(factory, "profile-user", "profile-user");
        await factory.ExecuteDbContextAsync(async db =>
        {
            await db.Profiles.AddAsync(new Profile
            {
                UserId = "profile-user",
                DisplayName = "Visible Name",
                Bio = "Visible bio"
            });
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateAuthenticatedClient("profile-user", "profile-user");

        var response = await client.GetAsync("/api/profiles/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        body.Should().NotBeNull();
        body!.DisplayName.Should().Be("Visible Name");
        body.Bio.Should().Be("Visible bio");
    }

    private static Task SeedUserAsync(
        IntegrationTestApplicationFactory factory,
        string userId,
        string userName)
    {
        return factory.ExecuteDbContextAsync(async db =>
        {
            await db.Users.AddAsync(new ApplicationUser
            {
                Id = userId,
                UserName = userName,
                Email = $"{userName}@example.test"
            });
            await db.SaveChangesAsync();
        });
    }
}
