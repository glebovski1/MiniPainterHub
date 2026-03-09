using FluentAssertions;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Tests.Infrastructure;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public class FeedControllerTests
{
    [Fact]
    public async Task GetFollowingFeed_ReturnsOnlyPostsFromFollowedUsers()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.SeedUserAsync("caller-user", "caller");
        await factory.SeedUserAsync("followed-user", "followed");
        await factory.SeedUserAsync("other-user", "other");
        await factory.ExecuteDbContextAsync(async db =>
        {
            db.Follows.Add(new Follow
            {
                FollowerUserId = "caller-user",
                FollowedUserId = "followed-user",
                CreatedUtc = System.DateTime.UtcNow
            });

            db.Posts.Add(TestData.CreatePost(1, "followed-user"));
            db.Posts.Add(TestData.CreatePost(2, "other-user"));
            await db.SaveChangesAsync();
        });

        using var client = factory.CreateAuthenticatedClient("caller-user", "caller");
        var response = await client.GetAsync("/api/feed/following?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<MiniPainterHub.Common.DTOs.PagedResult<MiniPainterHub.Common.DTOs.PostSummaryDto>>();
        payload.Should().NotBeNull();
        payload!.Items.Should().ContainSingle();
        payload.Items.Single().AuthorId.Should().Be("followed-user");
    }

    [Fact]
    public async Task GetFollowingFeed_ReturnsNewestPostsFirst()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.SeedUserAsync("caller-user", "caller");
        await factory.SeedUserAsync("followed-user", "followed");
        await factory.ExecuteDbContextAsync(async db =>
        {
            db.Follows.Add(new Follow
            {
                FollowerUserId = "caller-user",
                FollowedUserId = "followed-user",
                CreatedUtc = System.DateTime.UtcNow
            });

            var older = TestData.CreatePost(1, "followed-user");
            older.Title = "Older";
            older.CreatedUtc = System.DateTime.UtcNow.AddHours(-2);

            var newer = TestData.CreatePost(2, "followed-user");
            newer.Title = "Newer";
            newer.CreatedUtc = System.DateTime.UtcNow.AddHours(-1);

            db.Posts.AddRange(older, newer);
            await db.SaveChangesAsync();
        });

        using var client = factory.CreateAuthenticatedClient("caller-user", "caller");
        var payload = await client.GetFromJsonAsync<MiniPainterHub.Common.DTOs.PagedResult<MiniPainterHub.Common.DTOs.PostSummaryDto>>("/api/feed/following?page=1&pageSize=10");

        payload.Should().NotBeNull();
        payload!.Items.Select(post => post.Title).Should().ContainInOrder("Newer", "Older");
    }
}
