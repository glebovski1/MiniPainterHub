using System.Threading.Tasks;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class CommentMarkServiceTests
{
    [Fact]
    public async Task GetByCommentIdAsync_WhenCommentIsHiddenAndIncludeDeletedFalse_ThrowsNotFound()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1");
        var post = TestData.CreatePost(1, user.Id);
        var image = new PostImage { Id = 11, PostId = post.Id, ImageUrl = "https://img/1", Width = 1000, Height = 800 };
        var comment = TestData.CreateComment(21, post.Id, user.Id, isDeleted: true);
        var mark = new CommentImageMark
        {
            CommentId = comment.Id,
            PostImageId = image.Id,
            NormalizedX = 0.5m,
            NormalizedY = 0.5m
        };

        await context.Users.AddAsync(user);
        await context.Posts.AddAsync(post);
        await context.PostImages.AddAsync(image);
        await context.Comments.AddAsync(comment);
        await context.CommentImageMarks.AddAsync(mark);
        await context.SaveChangesAsync();

        var service = new CommentMarkService(context);

        var act = async () => await service.GetByCommentIdAsync(comment.Id, includeDeleted: false);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Comment mark not found.");
    }

    [Fact]
    public async Task UpsertAsync_WhenCommentNotOwnedByUser_ThrowsNotFound()
    {
        await using var context = AppDbContextFactory.Create();
        var author = TestData.CreateUser("author-1");
        var otherUser = TestData.CreateUser("user-2");
        var post = TestData.CreatePost(2, author.Id);
        var image = new PostImage { Id = 12, PostId = post.Id, ImageUrl = "https://img/2", Width = 1000, Height = 800 };
        var comment = TestData.CreateComment(22, post.Id, author.Id);

        await context.Users.AddRangeAsync(author, otherUser);
        await context.Posts.AddAsync(post);
        await context.PostImages.AddAsync(image);
        await context.Comments.AddAsync(comment);
        await context.SaveChangesAsync();

        var service = new CommentMarkService(context);

        var act = async () => await service.UpsertAsync(comment.Id, otherUser.Id, new ViewerMarkDraftDto
        {
            PostImageId = image.Id,
            NormalizedX = 0.25m,
            NormalizedY = 0.75m
        });

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Comment not found.");
    }

    [Fact]
    public async Task UpsertAsync_WhenCoordinatesAreOutOfRange_ThrowsValidationError()
    {
        await using var context = AppDbContextFactory.Create();
        var author = TestData.CreateUser("author-1");
        var post = TestData.CreatePost(3, author.Id);
        var image = new PostImage { Id = 13, PostId = post.Id, ImageUrl = "https://img/3", Width = 1000, Height = 800 };
        var comment = TestData.CreateComment(23, post.Id, author.Id);

        await context.Users.AddAsync(author);
        await context.Posts.AddAsync(post);
        await context.PostImages.AddAsync(image);
        await context.Comments.AddAsync(comment);
        await context.SaveChangesAsync();

        var service = new CommentMarkService(context);

        var act = async () => await service.UpsertAsync(comment.Id, author.Id, new ViewerMarkDraftDto
        {
            PostImageId = image.Id,
            NormalizedX = 1.2m,
            NormalizedY = -0.1m
        });

        var exception = await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("Viewer mark data is invalid.");

        exception.Which.Errors.Should().ContainKey("NormalizedX");
        exception.Which.Errors.Should().ContainKey("NormalizedY");
    }
}
