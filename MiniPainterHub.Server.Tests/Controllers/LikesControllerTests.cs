using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public class LikesControllerTests
{
    [Fact]
    public async Task GetLikes_ReturnsLikeContractWithUserFlag()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.ExecuteDbContextAsync(async db =>
        {
            var currentUser = new ApplicationUser
            {
                Id = "like-user",
                UserName = "like-user",
                Email = "like-user@example.test"
            };
            var otherUser = new ApplicationUser
            {
                Id = "other-user",
                UserName = "other-user",
                Email = "other-user@example.test"
            };

            await db.Users.AddRangeAsync(currentUser, otherUser);
            await db.Posts.AddAsync(new Post
            {
                Id = 202,
                Title = "Post",
                Content = "Body",
                CreatedById = currentUser.Id,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                IsDeleted = false
            });
            await db.Likes.AddRangeAsync(
                new Like { PostId = 202, UserId = currentUser.Id, CreatedAt = DateTime.UtcNow },
                new Like { PostId = 202, UserId = otherUser.Id, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateAuthenticatedClient("like-user", "like-user");

        var response = await client.GetAsync("/api/posts/202/likes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LikeDto>();
        body.Should().NotBeNull();
        body!.PostId.Should().Be(202);
        body.Count.Should().Be(2);
        body.UserHasLiked.Should().BeTrue();
    }

    [Fact]
    public async Task Like_WhenAuthenticated_ReturnsNoContentAndPersistsLike()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUsersAndPostAsync(factory, 203);
        using var client = factory.CreateAuthenticatedClient("like-user", "like-user");

        var response = await client.PostAsync("/api/posts/203/likes", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await factory.ExecuteDbContextAsync(async db =>
        {
            var like = await db.Likes.SingleAsync(l => l.PostId == 203 && l.UserId == "like-user");
            like.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task Unlike_WhenAuthenticated_ReturnsNoContentAndRemovesLike()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUsersAndPostAsync(factory, 204);
        await factory.ExecuteDbContextAsync(async db =>
        {
            await db.Likes.AddAsync(new Like
            {
                PostId = 204,
                UserId = "like-user",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateAuthenticatedClient("like-user", "like-user");

        var response = await client.DeleteAsync("/api/posts/204/likes");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await factory.ExecuteDbContextAsync(async db =>
        {
            (await db.Likes.CountAsync(l => l.PostId == 204 && l.UserId == "like-user")).Should().Be(0);
        });
    }

    private static Task SeedUsersAndPostAsync(IntegrationTestApplicationFactory factory, int postId)
    {
        return factory.ExecuteDbContextAsync(async db =>
        {
            var currentUser = new ApplicationUser
            {
                Id = "like-user",
                UserName = "like-user",
                Email = "like-user@example.test"
            };
            var otherUser = new ApplicationUser
            {
                Id = "other-user",
                UserName = "other-user",
                Email = "other-user@example.test"
            };

            await db.Users.AddRangeAsync(currentUser, otherUser);
            await db.Posts.AddAsync(new Post
            {
                Id = postId,
                Title = "Post",
                Content = "Body",
                CreatedById = currentUser.Id,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                IsDeleted = false
            });
            await db.SaveChangesAsync();
        });
    }
}
