using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class CommentServiceTests
{
    [Fact]
    public async Task CreateAsync_WhenPostIsMissing_ThrowsNotFoundException()
    {
        await using var context = AppDbContextFactory.Create();
        var service = new CommentService(context);

        var act = async () => await service.CreateAsync("user-1", 99, new MiniPainterHub.Common.DTOs.CreateCommentDto { Text = "Test" });

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Post not found.");
    }

    [Theory]
    [InlineData(0, 10, "page", "Page number must be at least 1.")]
    [InlineData(-1, 5, "page", "Page number must be at least 1.")]
    [InlineData(1, 0, "pageSize", "Page size must be greater than 0.")]
    [InlineData(2, -3, "pageSize", "Page size must be greater than 0.")]
    public async Task GetByPostIdAsync_WhenPaginationIsInvalid_ThrowsDomainValidationException(
        int page,
        int pageSize,
        string expectedKey,
        string expectedMessage)
    {
        await using var context = AppDbContextFactory.Create();
        var service = new CommentService(context);

        var act = async () => await service.GetByPostIdAsync(1, page, pageSize);

        var exception = await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("Pagination parameters are invalid.");

        exception.Which.Errors.Should().ContainKey(expectedKey)
            .WhoseValue.Should().Contain(expectedMessage);
    }

    [Fact]
    public async Task GetByPostIdAsync_WhenPageAndPageSizeInvalid_ThrowsDomainValidationExceptionWithAllErrors()
    {
        await using var context = AppDbContextFactory.Create();
        var service = new CommentService(context);

        var act = async () => await service.GetByPostIdAsync(1, 0, 0);

        var exception = await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("Pagination parameters are invalid.");

        exception.Which.Errors.Should()
            .ContainKey("page").WhoseValue.Should().Contain("Page number must be at least 1.");
        exception.Which.Errors.Should()
            .ContainKey("pageSize").WhoseValue.Should().Contain("Page size must be greater than 0.");
    }

    [Fact]
    public async Task DeleteAsync_WhenCommentNotOwnedAndNotAdmin_ThrowsNotFoundException()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1");
        var otherUser = TestData.CreateUser("user-2");
        var post = TestData.CreatePost(1, user.Id);
        var comment = TestData.CreateComment(1, post.Id, otherUser.Id);
        await context.Users.AddRangeAsync(user, otherUser);
        await context.Posts.AddAsync(post);
        await context.Comments.AddAsync(comment);
        await context.SaveChangesAsync();
        var service = new CommentService(context);

        var act = async () => await service.DeleteAsync(comment.Id, user.Id, isAdmin: false);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Comment not found.");
    }

    [Fact]
    public async Task UpdateAsync_WhenCommentExistsForUser_UpdatesContent()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1");
        var post = TestData.CreatePost(1, user.Id);
        var comment = TestData.CreateComment(1, post.Id, user.Id);
        await context.Users.AddAsync(user);
        await context.Posts.AddAsync(post);
        await context.Comments.AddAsync(comment);
        await context.SaveChangesAsync();
        var service = new CommentService(context);
        var dto = new MiniPainterHub.Common.DTOs.UpdateCommentDto { Content = "Updated" };

        var result = await service.UpdateAsync(comment.Id, user.Id, dto);

        result.Should().BeTrue();
        (await context.Comments.SingleAsync()).Text.Should().Be("Updated");
    }
}
