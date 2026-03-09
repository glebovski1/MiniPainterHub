using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Tests.Infrastructure;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public class FollowsControllerTests
{
    [Fact]
    public async Task Follow_WhenAuthenticated_CreatesRelationship()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.SeedUserAsync("caller-user", "caller");
        await factory.SeedUserAsync("target-user", "target");
        await factory.SeedProfileAsync("target-user", "Target Painter", "Bio");
        using var client = factory.CreateAuthenticatedClient("caller-user", "caller");

        var response = await client.PostAsync("/api/follows/target-user", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var following = await client.GetFromJsonAsync<MiniPainterHub.Common.DTOs.UserListItemDto[]>("/api/follows/me/following");
        following.Should().NotBeNull();
        following!.Single().UserId.Should().Be("target-user");
    }

    [Fact]
    public async Task Follow_WhenAlreadyFollowing_ReturnsConflictProblemDetails()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.SeedUserAsync("caller-user", "caller");
        await factory.SeedUserAsync("target-user", "target");
        await factory.ExecuteDbContextAsync(async db =>
        {
            db.Follows.Add(new Follow
            {
                FollowerUserId = "caller-user",
                FollowedUserId = "target-user",
                CreatedUtc = System.DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        using var client = factory.CreateAuthenticatedClient("caller-user", "caller");
        var response = await client.PostAsync("/api/follows/target-user", null);

        await ProblemDetailsAssertions.AssertAsync(
            response,
            HttpStatusCode.Conflict,
            "Conflict",
            "You already follow this user.");
    }

    [Fact]
    public async Task Follow_WhenTargetIsSelf_ReturnsValidationProblemDetails()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.SeedUserAsync("caller-user", "caller");
        using var client = factory.CreateAuthenticatedClient("caller-user", "caller");

        var response = await client.PostAsync("/api/follows/caller-user", null);

        await ProblemDetailsAssertions.AssertAsync(
            response,
            HttpStatusCode.BadRequest,
            "Validation error",
            expectedErrorKeys: new[] { "userId" });
    }

    [Fact]
    public async Task Unfollow_WhenRelationshipExists_RemovesRelationship()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.SeedUserAsync("caller-user", "caller");
        await factory.SeedUserAsync("target-user", "target");
        await factory.ExecuteDbContextAsync(async db =>
        {
            db.Follows.Add(new Follow
            {
                FollowerUserId = "caller-user",
                FollowedUserId = "target-user",
                CreatedUtc = System.DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        using var client = factory.CreateAuthenticatedClient("caller-user", "caller");
        var response = await client.DeleteAsync("/api/follows/target-user");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var following = await client.GetFromJsonAsync<UserListItemDto[]>("/api/follows/me/following");
        following.Should().BeEmpty();
    }

    [Fact]
    public async Task FollowersAndFollowingEndpoints_ReturnExpectedUsers()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.SeedUserAsync("caller-user", "caller");
        await factory.SeedUserAsync("follower-user", "follower");
        await factory.SeedUserAsync("following-user", "following");
        await factory.SeedProfileAsync("follower-user", "Follower Painter", null);
        await factory.SeedProfileAsync("following-user", "Following Painter", null);
        await factory.ExecuteDbContextAsync(async db =>
        {
            db.Follows.Add(new Follow
            {
                FollowerUserId = "follower-user",
                FollowedUserId = "caller-user",
                CreatedUtc = System.DateTime.UtcNow.AddMinutes(-1)
            });
            db.Follows.Add(new Follow
            {
                FollowerUserId = "caller-user",
                FollowedUserId = "following-user",
                CreatedUtc = System.DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        using var client = factory.CreateAuthenticatedClient("caller-user", "caller");
        var followers = await client.GetFromJsonAsync<UserListItemDto[]>("/api/follows/me/followers");
        var following = await client.GetFromJsonAsync<UserListItemDto[]>("/api/follows/me/following");

        followers.Should().ContainSingle();
        followers!.Single().DisplayName.Should().Be("Follower Painter");
        following.Should().ContainSingle();
        following!.Single().DisplayName.Should().Be("Following Painter");
    }
}
