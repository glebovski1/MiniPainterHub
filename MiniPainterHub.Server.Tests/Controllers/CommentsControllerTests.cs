using System;
using System.Net;
using System.Net.Http.Json;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
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
        await factory.SeedUserAndPostAsync("comment-user", 101);
        await factory.SeedCommentAsync(501, 101, "comment-user", "First comment");
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
        comment.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task GetByPost_WhenAnonymousRequestsHiddenComments_ReturnsForbidden()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.SeedUserAndPostAsync("comment-user", 106);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/posts/106/comments?page=1&pageSize=10&includeDeleted=true");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetByPost_WhenModeratorRequestsHiddenOnly_ReturnsDeletedComments()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.SeedUserAndPostAsync("comment-user", 107);
        await factory.SeedUserAsync("mod-user", "mod-user");
        await factory.SeedCommentAsync(801, 107, "comment-user", "Visible comment", isDeleted: false);
        await factory.SeedCommentAsync(802, 107, "comment-user", "Hidden comment", isDeleted: true);
        using var client = factory.CreateAuthenticatedClient("mod-user", "mod-user", "Moderator");

        var response = await client.GetAsync("/api/posts/107/comments?page=1&pageSize=10&deletedOnly=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<CommentDto>>();
        body.Should().NotBeNull();
        body!.Items.Should().ContainSingle();
        body.Items.Single().Id.Should().Be(802);
        body.Items.Single().IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Create_WhenAuthenticated_ReturnsCreatedCommentContract()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.SeedUserAndPostAsync("author-user", 102);
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
        await factory.SeedUserAndPostAsync("author-user", 103);
        await factory.SeedCommentAsync(700, 103, "author-user", "Before");
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
        await factory.SeedUserAndPostAsync("author-user", 104);
        await factory.SeedCommentAsync(701, 104, "author-user", "Delete me");
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
        await factory.SeedUserAndPostAsync("author-user", 105);
        await factory.SeedCommentAsync(702, 105, "author-user", "Lookup comment");
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

        await ProblemDetailsAssertions.AssertAsync(
            response,
            HttpStatusCode.NotFound,
            "Not found",
            "Comment not found.");
    }
}
