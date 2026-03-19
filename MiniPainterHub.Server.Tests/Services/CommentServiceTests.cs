using System.Linq;
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
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task CreateAsync_WhenUserIdIsMissing_ThrowsUnauthorizedAccessException(string? userId)
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1");
        var post = TestData.CreatePost(1, user.Id);
        await context.Users.AddAsync(user);
        await context.Posts.AddAsync(post);
        await context.SaveChangesAsync();
        var service = new CommentService(context);

        var act = async () => await service.CreateAsync(userId!, post.Id, new MiniPainterHub.Common.DTOs.CreateCommentDto
        {
            Text = "Test"
        });

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User must be authenticated to create comments.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_WhenCommentTextIsBlank_ThrowsDomainValidationException(string text)
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1");
        var post = TestData.CreatePost(1, user.Id);
        await context.Users.AddAsync(user);
        await context.Posts.AddAsync(post);
        await context.SaveChangesAsync();
        var service = new CommentService(context);

        var act = async () => await service.CreateAsync(user.Id, post.Id, new MiniPainterHub.Common.DTOs.CreateCommentDto
        {
            Text = text
        });

        var exception = await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("Comment data is invalid.");

        exception.Which.Errors.Should().ContainKey("text")
            .WhoseValue.Should().Contain("Comment text is required.");
        context.Comments.Should().BeEmpty();
    }

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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task DeleteAsync_WhenUserIdIsMissing_ThrowsUnauthorizedAccessException(string? userId)
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

        var act = async () => await service.DeleteAsync(comment.Id, userId!, isAdmin: false);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User must be authenticated to delete comments.");
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task UpdateAsync_WhenUserIdIsMissing_ThrowsUnauthorizedAccessException(string? userId)
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

        var act = async () => await service.UpdateAsync(comment.Id, userId!, new MiniPainterHub.Common.DTOs.UpdateCommentDto
        {
            Content = "Updated"
        });

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User must be authenticated to update comments.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_WhenCommentTextIsBlank_ThrowsDomainValidationException(string content)
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

        var act = async () => await service.UpdateAsync(comment.Id, user.Id, new MiniPainterHub.Common.DTOs.UpdateCommentDto
        {
            Content = content
        });

        var exception = await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("Comment data is invalid.");

        exception.Which.Errors.Should().ContainKey("content")
            .WhoseValue.Should().Contain("Comment text is required.");
        (await context.Comments.SingleAsync()).Text.Should().Be("Comment 1");
    }

    [Fact]
    public async Task GetByIdAsync_WhenCommentExists_ReturnsComment()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1", "commenter");
        var post = TestData.CreatePost(1, user.Id);
        var comment = TestData.CreateComment(7, post.Id, user.Id);
        await context.Users.AddAsync(user);
        await context.Posts.AddAsync(post);
        await context.Comments.AddAsync(comment);
        await context.SaveChangesAsync();
        var service = new CommentService(context);

        var result = await service.GetByIdAsync(comment.Id);

        result.Id.Should().Be(comment.Id);
        result.PostId.Should().Be(post.Id);
        result.AuthorId.Should().Be(user.Id);
        result.AuthorName.Should().Be(user.UserName);
        result.Content.Should().Be(comment.Text);
        result.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_WhenCommentMissing_ThrowsNotFoundException()
    {
        await using var context = AppDbContextFactory.Create();
        var service = new CommentService(context);

        var act = async () => await service.GetByIdAsync(777);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Comment not found.");
    }

    [Fact]
    public async Task GetByPostIdAsync_WhenIncludeDeletedTrue_ReturnsVisibleAndHiddenComments()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1", "commenter");
        var post = TestData.CreatePost(1, user.Id);
        var visibleComment = TestData.CreateComment(20, post.Id, user.Id, isDeleted: false);
        var hiddenComment = TestData.CreateComment(21, post.Id, user.Id, isDeleted: true);
        await context.Users.AddAsync(user);
        await context.Posts.AddAsync(post);
        await context.Comments.AddRangeAsync(visibleComment, hiddenComment);
        await context.SaveChangesAsync();
        var service = new CommentService(context);

        var result = await service.GetByPostIdAsync(post.Id, 1, 20, includeDeleted: true, deletedOnly: false);

        result.Items.Should().HaveCount(2);
        result.Items.Should().Contain(item => item.Id == visibleComment.Id && !item.IsDeleted);
        result.Items.Should().Contain(item => item.Id == hiddenComment.Id && item.IsDeleted);
    }

    [Fact]
    public async Task GetByPostIdAsync_WhenDeletedOnlyTrue_ReturnsOnlyHiddenComments()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1", "commenter");
        var post = TestData.CreatePost(2, user.Id);
        var visibleComment = TestData.CreateComment(30, post.Id, user.Id, isDeleted: false);
        var hiddenComment = TestData.CreateComment(31, post.Id, user.Id, isDeleted: true);
        await context.Users.AddAsync(user);
        await context.Posts.AddAsync(post);
        await context.Comments.AddRangeAsync(visibleComment, hiddenComment);
        await context.SaveChangesAsync();
        var service = new CommentService(context);

        var result = await service.GetByPostIdAsync(post.Id, 1, 20, includeDeleted: true, deletedOnly: true);

        result.Items.Should().ContainSingle();
        result.Items.Single().Id.Should().Be(hiddenComment.Id);
        result.Items.Single().IsDeleted.Should().BeTrue();
    }
}
