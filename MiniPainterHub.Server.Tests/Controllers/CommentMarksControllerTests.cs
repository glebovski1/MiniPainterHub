using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public class CommentMarksControllerTests
{
    [Fact]
    public async Task GetByCommentId_WhenModeratorIncludesDeleted_ReturnsHiddenCommentMark()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.SeedUserAsync("comment-user", "comment-user");
        await factory.SeedUserAsync("moderator-user", "moderator-user");
        await factory.ExecuteDbContextAsync(async db =>
        {
            var post = TestData.CreatePost(501, "comment-user");
            var image = new PostImage
            {
                Id = 601,
                PostId = post.Id,
                ImageUrl = "https://img/hidden-mark",
                Width = 1600,
                Height = 900
            };
            var comment = TestData.CreateComment(701, post.Id, "comment-user", isDeleted: true);
            var mark = new CommentImageMark
            {
                CommentId = comment.Id,
                PostImageId = image.Id,
                NormalizedX = 0.3m,
                NormalizedY = 0.7m,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };

            await db.Posts.AddAsync(post);
            await db.PostImages.AddAsync(image);
            await db.Comments.AddAsync(comment);
            await db.CommentImageMarks.AddAsync(mark);
            await db.SaveChangesAsync();
        });

        using var client = factory.CreateAuthenticatedClient("moderator-user", "moderator-user", "Moderator");

        var response = await client.GetAsync("/api/comments/701/mark?includeDeleted=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CommentMarkDto>();
        body.Should().NotBeNull();
        body!.CommentId.Should().Be(701);
        body.PostImageId.Should().Be(601);
    }

    [Fact]
    public async Task GetByCommentId_WhenRegularUserIncludesDeleted_ReturnsForbidden()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.SeedUserAsync("comment-user", "comment-user");
        await factory.SeedUserAsync("reader-user", "reader-user");
        await factory.ExecuteDbContextAsync(async db =>
        {
            var post = TestData.CreatePost(502, "comment-user");
            var image = new PostImage
            {
                Id = 602,
                PostId = post.Id,
                ImageUrl = "https://img/hidden-mark-2",
                Width = 1600,
                Height = 900
            };
            var comment = TestData.CreateComment(702, post.Id, "comment-user", isDeleted: true);
            var mark = new CommentImageMark
            {
                CommentId = comment.Id,
                PostImageId = image.Id,
                NormalizedX = 0.4m,
                NormalizedY = 0.6m,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };

            await db.Posts.AddAsync(post);
            await db.PostImages.AddAsync(image);
            await db.Comments.AddAsync(comment);
            await db.CommentImageMarks.AddAsync(mark);
            await db.SaveChangesAsync();
        });

        using var client = factory.CreateAuthenticatedClient("reader-user", "reader-user");

        var response = await client.GetAsync("/api/comments/702/mark?includeDeleted=true");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
