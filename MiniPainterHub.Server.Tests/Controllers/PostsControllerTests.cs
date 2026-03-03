using System;
using System.Linq;
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

public class PostsControllerTests
{
    [Fact]
    public async Task GetAll_WhenAnonymous_ReturnsPagedPostsContract()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "post-user", "post-user");
        await SeedPostAsync(factory, 300, "post-user", "Landscape", "Painted mountain");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/posts?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<PostSummaryDto>>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().Be(1);
        body.PageNumber.Should().Be(1);
        body.PageSize.Should().Be(10);
        body.Items.Should().ContainSingle();
        body.Items.Single().Title.Should().Be("Landscape");
        body.Items.Single().AuthorId.Should().Be("post-user");
    }

    [Fact]
    public async Task GetById_WhenAnonymous_ReturnsPostContract()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "post-user", "post-user");
        await SeedPostAsync(factory, 301, "post-user", "Portrait", "Painted portrait");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/posts/301");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PostDto>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(301);
        body.Title.Should().Be("Portrait");
        body.CreatedById.Should().Be("post-user");
        body.AuthorName.Should().Be("post-user");
    }

    [Fact]
    public async Task Create_WhenAuthenticated_ReturnsCreatedAndPersistsPost()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "post-user", "post-user");
        using var client = factory.CreateAuthenticatedClient("post-user", "post-user");

        var response = await client.PostAsJsonAsync("/api/posts", new CreatePostDto
        {
            Title = "New post title",
            Content = "New post content"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<PostDto>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("New post title");
        body.CreatedById.Should().Be("post-user");

        await factory.ExecuteDbContextAsync(async db =>
        {
            (await db.Posts.CountAsync()).Should().Be(1);
            var post = await db.Posts.SingleAsync();
            post.Title.Should().Be("New post title");
        });
    }

    [Fact]
    public async Task Update_WhenOwnerAuthenticated_ReturnsNoContentAndUpdatesPost()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "post-user", "post-user");
        await SeedPostAsync(factory, 302, "post-user", "Before", "Before content");
        using var client = factory.CreateAuthenticatedClient("post-user", "post-user");

        var response = await client.PutAsJsonAsync("/api/posts/302", new UpdatePostDto
        {
            Title = "After",
            Content = "After content"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await factory.ExecuteDbContextAsync(async db =>
        {
            var post = await db.Posts.FindAsync(302);
            post.Should().NotBeNull();
            post!.Title.Should().Be("After");
            post.Content.Should().Be("After content");
        });
    }

    [Fact]
    public async Task Delete_WhenOwnerAuthenticated_ReturnsNoContentAndSoftDeletesPost()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "post-user", "post-user");
        await SeedPostAsync(factory, 303, "post-user", "Delete me", "Delete content");
        using var client = factory.CreateAuthenticatedClient("post-user", "post-user");

        var response = await client.DeleteAsync("/api/posts/303");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await factory.ExecuteDbContextAsync(async db =>
        {
            var post = await db.Posts.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == 303);
            post.Should().NotBeNull();
            post!.Status.Should().Be(MiniPainterHub.Server.Entities.ContentStatus.SoftDeleted);
        });
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

    private static Task SeedPostAsync(
        IntegrationTestApplicationFactory factory,
        int postId,
        string userId,
        string title,
        string content)
    {
        return factory.ExecuteDbContextAsync(async db =>
        {
            await db.Posts.AddAsync(new Post
            {
                Id = postId,
                Title = title,
                Content = content,
                CreatedById = userId,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                Status = MiniPainterHub.Server.Entities.ContentStatus.Active
            });
            await db.SaveChangesAsync();
        });
    }
}
