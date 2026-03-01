using System;
using System.Net;
using System.Net.Http.Json;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public class CommentsControllerTests
{
    [Fact]
    public async Task GetByPost_ReturnsPagedCommentsContract()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.ExecuteDbContextAsync(async db =>
        {
            var author = new ApplicationUser
            {
                Id = "comment-user",
                UserName = "comment-user",
                Email = "comment-user@example.test"
            };
            await db.Users.AddAsync(author);
            await db.Posts.AddAsync(new Post
            {
                Id = 101,
                Title = "Post",
                Content = "Post body",
                CreatedById = author.Id,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                IsDeleted = false
            });
            await db.Comments.AddAsync(new Comment
            {
                Id = 501,
                PostId = 101,
                AuthorId = author.Id,
                Text = "First comment",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                IsDeleted = false
            });
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/posts/101/comments?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<CommentDto>>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().Be(1);
        body.PageNumber.Should().Be(1);
        body.PageSize.Should().Be(10);
        body.Items.Should().ContainSingle();
        var comment = body.Items.Single();
        comment.Id.Should().Be(501);
        comment.PostId.Should().Be(101);
        comment.Content.Should().Be("First comment");
        comment.AuthorName.Should().Be("comment-user");
    }

    [Fact]
    public async Task Create_WhenAuthenticated_ReturnsCreatedCommentContract()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAndPostAsync(factory, "author-user", 102);
        using var client = factory.CreateAuthenticatedClient("author-user", "author-user");

        var response = await client.PostAsJsonAsync("/api/posts/102/comments", new CreateCommentDto
        {
            PostId = 102,
            Text = "New comment"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CommentDto>();
        body.Should().NotBeNull();
        body!.PostId.Should().Be(102);
        body.AuthorId.Should().Be("author-user");
        body.Content.Should().Be("New comment");
    }

    [Fact]
    public async Task Update_WhenAuthenticated_ReturnsNoContentAndUpdatesComment()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAndPostAsync(factory, "author-user", 103);
        await factory.ExecuteDbContextAsync(async db =>
        {
            await db.Comments.AddAsync(new Comment
            {
                Id = 700,
                PostId = 103,
                AuthorId = "author-user",
                Text = "Before",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                IsDeleted = false
            });
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateAuthenticatedClient("author-user", "author-user");

        var response = await client.PutAsJsonAsync("/api/comments/700", new UpdateCommentDto
        {
            Content = "After"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await factory.ExecuteDbContextAsync(async db =>
        {
            (await db.Comments.FindAsync(700))!.Text.Should().Be("After");
        });
    }

    [Fact]
    public async Task Delete_WhenAuthenticated_ReturnsNoContentAndSoftDeletesComment()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAndPostAsync(factory, "author-user", 104);
        await factory.ExecuteDbContextAsync(async db =>
        {
            await db.Comments.AddAsync(new Comment
            {
                Id = 701,
                PostId = 104,
                AuthorId = "author-user",
                Text = "Delete me",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                IsDeleted = false
            });
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateAuthenticatedClient("author-user", "author-user");

        var response = await client.DeleteAsync("/api/comments/701");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await factory.ExecuteDbContextAsync(async db =>
        {
            (await db.Comments.FindAsync(701))!.IsDeleted.Should().BeTrue();
        });
    }

    [Fact]
    public async Task GetById_WhenAuthenticated_ReturnsCommentContract()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAndPostAsync(factory, "author-user", 105);
        await factory.ExecuteDbContextAsync(async db =>
        {
            await db.Comments.AddAsync(new Comment
            {
                Id = 702,
                PostId = 105,
                AuthorId = "author-user",
                Text = "Lookup comment",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                IsDeleted = false
            });
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateAuthenticatedClient("author-user", "author-user");

        var response = await client.GetAsync("/api/comments/702");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CommentDto>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(702);
        body.PostId.Should().Be(105);
        body.AuthorId.Should().Be("author-user");
        body.Content.Should().Be("Lookup comment");
    }

    [Fact]
    public async Task GetById_WhenCommentMissing_ReturnsNotFoundProblemDetails()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient("author-user", "author-user");

        var response = await client.GetAsync("/api/comments/999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("title").GetString().Should().Be("Not found");
        json.RootElement.GetProperty("detail").GetString().Should().Be("Comment not found.");
    }

    private static Task SeedUserAndPostAsync(
        IntegrationTestApplicationFactory factory,
        string userId,
        int postId)
    {
        return factory.ExecuteDbContextAsync(async db =>
        {
            var author = new ApplicationUser
            {
                Id = userId,
                UserName = userId,
                Email = $"{userId}@example.test"
            };

            await db.Users.AddAsync(author);
            await db.Posts.AddAsync(new Post
            {
                Id = postId,
                Title = "Post",
                Content = "Post body",
                CreatedById = author.Id,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                IsDeleted = false
            });
            await db.SaveChangesAsync();
        });
    }
}
