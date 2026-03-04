using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Tests.Infrastructure;
using Moq;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class ModerationServiceTests
{
    [Fact]
    public async Task ModeratePostAsync_WhenRestoringPostDeletedOutsideModeration_ThrowsDomainValidationException()
    {
        await using var context = AppDbContextFactory.Create();
        var post = TestData.CreatePost(1, "author-1", isDeleted: true);
        post.ModeratedByUserId = null;
        post.ModeratedUtc = null;
        post.SoftDeletedUtc = DateTime.UtcNow.AddMinutes(-10);
        await context.Posts.AddAsync(post);
        await context.SaveChangesAsync();

        var service = new ModerationService(context, CreateUserManagerMock().Object);

        var act = async () => await service.ModeratePostAsync(post.Id, "moderator-1", hide: false, reason: "restore");

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.Errors.Should().ContainKey("postId");

        var stored = await context.Posts.SingleAsync(p => p.Id == post.Id);
        stored.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task ModerateCommentAsync_WhenRestoringLegacyDeletedComment_AllowsRestore()
    {
        await using var context = AppDbContextFactory.Create();
        var comment = TestData.CreateComment(11, 3, "author-1", isDeleted: true);
        comment.SoftDeletedUtc = null;
        comment.ModeratedByUserId = null;
        comment.ModeratedUtc = null;
        await context.Comments.AddAsync(comment);
        await context.SaveChangesAsync();

        var userManager = CreateUserManagerMock();
        userManager.Setup(m => m.FindByIdAsync("admin-1"))
            .ReturnsAsync(TestData.CreateUser("admin-1", "admin-1"));
        userManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(["Admin"]);
        var service = new ModerationService(context, userManager.Object);

        await service.ModerateCommentAsync(comment.Id, "admin-1", hide: false, reason: "legacy restore");

        var stored = await context.Comments.SingleAsync(c => c.Id == comment.Id);
        stored.IsDeleted.Should().BeFalse();
        stored.ModeratedByUserId.Should().Be("admin-1");
    }

    [Fact]
    public async Task ModeratePostAsync_WhenUserDeletedAfterModerationHide_ThrowsDomainValidationException()
    {
        await using var context = AppDbContextFactory.Create();
        var post = TestData.CreatePost(2, "author-1", isDeleted: true);
        post.ModeratedByUserId = "mod-1";
        post.ModeratedUtc = DateTime.UtcNow.AddMinutes(-15);
        post.SoftDeletedUtc = DateTime.UtcNow.AddMinutes(-5);
        await context.Posts.AddAsync(post);
        await context.SaveChangesAsync();

        var service = new ModerationService(context, CreateUserManagerMock().Object);

        var act = async () => await service.ModeratePostAsync(post.Id, "mod-1", hide: false, reason: "restore");

        await act.Should().ThrowAsync<DomainValidationException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("2020-01-01T00:00:00Z")]
    public async Task SuspendUserAsync_WhenSuspensionExpiryIsNotFuture_ThrowsDomainValidationException(string? suspendedUntilRaw)
    {
        await using var context = AppDbContextFactory.Create();
        var userManagerMock = CreateUserManagerMock();
        var service = new ModerationService(context, userManagerMock.Object);

        DateTime? suspendedUntilUtc = suspendedUntilRaw is null
            ? null
            : DateTime.Parse(suspendedUntilRaw, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

        var act = async () => await service.SuspendUserAsync("target-1", "admin-1", suspendedUntilUtc, "reason");

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.Errors.Should().ContainKey("suspendedUntilUtc");
        userManagerMock.Verify(m => m.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
    }
}
