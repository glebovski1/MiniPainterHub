using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
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

    [Fact]
    public async Task ModeratePostAsync_WhenHidingPostDeletedOutsideModeration_ThrowsDomainValidationException()
    {
        await using var context = AppDbContextFactory.Create();
        var post = TestData.CreatePost(20, "author-1", isDeleted: true);
        post.SoftDeletedUtc = DateTime.UtcNow.AddMinutes(-10);
        post.ModeratedByUserId = null;
        post.ModeratedUtc = null;
        await context.Posts.AddAsync(post);
        await context.SaveChangesAsync();

        var service = new ModerationService(context, CreateUserManagerMock().Object);

        var act = async () => await service.ModeratePostAsync(post.Id, "mod-1", hide: true, reason: "spam");

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.Errors.Should().ContainKey("postId");

        var stored = await context.Posts.SingleAsync(p => p.Id == post.Id);
        stored.ModeratedByUserId.Should().BeNull();
        stored.ModerationReason.Should().BeNull();
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

    [Fact]
    public async Task SuspendUserAsync_WhenTargetIsActor_ThrowsDomainValidationException()
    {
        await using var context = AppDbContextFactory.Create();
        var userManager = CreateUserManagerMock();
        userManager.Setup(m => m.FindByIdAsync("admin-1"))
            .ReturnsAsync(TestData.CreateUser("admin-1", "admin-1"));
        var service = new ModerationService(context, userManager.Object);

        var act = async () => await service.SuspendUserAsync("admin-1", "admin-1", DateTime.UtcNow.AddDays(1), "reason");

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.Errors.Should().ContainKey("targetUserId");
        userManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task SuspendUserAsync_WhenTargetHasStaffRole_ThrowsDomainValidationException()
    {
        await using var context = AppDbContextFactory.Create();
        var target = TestData.CreateUser("mod-1", "mod-1");
        var userManager = CreateUserManagerMock();
        userManager.Setup(m => m.FindByIdAsync("mod-1"))
            .ReturnsAsync(target);
        userManager.Setup(m => m.GetRolesAsync(target))
            .ReturnsAsync(["Moderator"]);
        var service = new ModerationService(context, userManager.Object);

        var act = async () => await service.SuspendUserAsync("mod-1", "admin-1", DateTime.UtcNow.AddDays(1), "reason");

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.Errors.Should().ContainKey("targetUserId");
        userManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task SuspendUserAsync_WhenIdentityUpdateFails_ThrowsDomainValidationException()
    {
        await using var context = AppDbContextFactory.Create();
        var target = TestData.CreateUser("user-1", "user-1");
        var userManager = CreateUserManagerMock();
        userManager.Setup(m => m.FindByIdAsync("user-1"))
            .ReturnsAsync(target);
        userManager.Setup(m => m.GetRolesAsync(target))
            .ReturnsAsync(Array.Empty<string>());
        userManager.Setup(m => m.UpdateAsync(target))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "Store", Description = "Write failed." }));
        var service = new ModerationService(context, userManager.Object);

        var act = async () => await service.SuspendUserAsync("user-1", "admin-1", DateTime.UtcNow.AddDays(1), "reason");

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.Errors.Should().ContainKey("Store");
    }

    [Theory]
    [InlineData(0, 20, "page")]
    [InlineData(-1, 20, "page")]
    [InlineData(1, 0, "pageSize")]
    [InlineData(1, -5, "pageSize")]
    [InlineData(1, 101, "pageSize")]
    public async Task GetAuditAsync_WhenPaginationIsInvalid_ThrowsDomainValidationException(int page, int pageSize, string errorKey)
    {
        await using var context = AppDbContextFactory.Create();
        var service = new ModerationService(context, CreateUserManagerMock().Object);

        var act = async () => await service.GetAuditAsync(new ModerationAuditQueryDto
        {
            Page = page,
            PageSize = pageSize
        });

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.Errors.Should().ContainKey(errorKey);
    }

    [Fact]
    public async Task ModeratePostAsync_WhenActorHasMultipleRoles_PrefersAdminInAuditLog()
    {
        await using var context = AppDbContextFactory.Create();
        var post = TestData.CreatePost(12, "author-1", isDeleted: false);
        await context.Posts.AddAsync(post);
        await context.SaveChangesAsync();

        var userManager = CreateUserManagerMock();
        userManager.Setup(m => m.FindByIdAsync("actor-1"))
            .ReturnsAsync(TestData.CreateUser("actor-1", "actor-1"));
        userManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(["User", "Admin"]);
        var service = new ModerationService(context, userManager.Object);

        await service.ModeratePostAsync(post.Id, "actor-1", hide: true, reason: "policy");

        var audit = await context.ModerationAuditLogs.SingleAsync();
        audit.ActorRole.Should().Be("Admin");
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
