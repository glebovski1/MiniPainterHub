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

public class ModerationControllerTests
{
    [Fact]
    public async Task HidePost_WhenModeratorAuthenticated_SoftDeletesPostAndWritesAudit()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "mod-1", "mod-1");
        await SeedUserAsync(factory, "author-1", "author-1");
        await SeedPostAsync(factory, 901, "author-1");
        using var client = factory.CreateAuthenticatedClient("mod-1", "mod-1", "Moderator");

        var response = await client.PostAsJsonAsync("/api/moderation/posts/901/hide", new ModerationActionRequestDto
        {
            Reason = "policy violation"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await factory.ExecuteDbContextAsync(async db =>
        {
            var post = await db.Posts.SingleAsync(p => p.Id == 901);
            post.IsDeleted.Should().BeTrue();
            post.SoftDeletedUtc.Should().NotBeNull();
            post.ModeratedByUserId.Should().Be("mod-1");
            post.ModerationReason.Should().Be("policy violation");

            var audit = await db.ModerationAuditLogs.SingleAsync();
            audit.ActionType.Should().Be("PostHide");
            audit.TargetType.Should().Be("Post");
            audit.TargetId.Should().Be("901");
            audit.ActorUserId.Should().Be("mod-1");
        });
    }

    [Fact]
    public async Task RestoreComment_WhenAdminAuthenticated_RestoresCommentAndWritesAudit()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "admin-1", "admin-1");
        await SeedUserAsync(factory, "author-2", "author-2");
        await SeedPostAsync(factory, 902, "author-2");
        await SeedCommentAsync(factory, 9021, 902, "author-2", isDeleted: true);
        using var client = factory.CreateAuthenticatedClient("admin-1", "admin-1", "Admin");

        var response = await client.PostAsJsonAsync("/api/moderation/comments/9021/restore", new ModerationActionRequestDto
        {
            Reason = "appeal accepted"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await factory.ExecuteDbContextAsync(async db =>
        {
            var comment = await db.Comments.SingleAsync(c => c.Id == 9021);
            comment.IsDeleted.Should().BeFalse();
            comment.SoftDeletedUtc.Should().BeNull();
            comment.ModeratedByUserId.Should().Be("admin-1");
            comment.ModerationReason.Should().Be("appeal accepted");

            var audit = await db.ModerationAuditLogs.SingleAsync();
            audit.ActionType.Should().Be("CommentRestore");
            audit.TargetType.Should().Be("Comment");
            audit.TargetId.Should().Be("9021");
        });
    }

    [Fact]
    public async Task SuspendAndUnsuspendUser_WhenAdminAuthenticated_UpdatesUserAndWritesAuditRows()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "admin-2", "admin-2");
        await SeedUserAsync(factory, "target-1", "target-1");
        using var client = factory.CreateAuthenticatedClient("admin-2", "admin-2", "Admin");

        var suspendUntil = DateTime.UtcNow.AddDays(3);
        var suspendResponse = await client.PostAsJsonAsync("/api/moderation/users/target-1/suspend", new SuspendUserRequestDto
        {
            SuspendedUntilUtc = suspendUntil,
            Reason = "spam wave"
        });
        suspendResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var unsuspendResponse = await client.PostAsJsonAsync("/api/moderation/users/target-1/unsuspend", new ModerationActionRequestDto
        {
            Reason = "manual review complete"
        });
        unsuspendResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await factory.ExecuteDbContextAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == "target-1");
            user.SuspendedUntilUtc.Should().BeNull();
            user.SuspensionReason.Should().Be("manual review complete");
            user.SuspensionUpdatedUtc.Should().NotBeNull();

            var auditRows = await db.ModerationAuditLogs.OrderBy(a => a.Id).ToListAsync();
            auditRows.Should().HaveCount(2);
            auditRows[0].ActionType.Should().Be("UserSuspend");
            auditRows[1].ActionType.Should().Be("UserUnsuspend");
        });
    }

    [Fact]
    public async Task SuspendUser_WhenRoleIsNotAdmin_ReturnsForbidden()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "mod-2", "mod-2");
        await SeedUserAsync(factory, "target-2", "target-2");
        using var client = factory.CreateAuthenticatedClient("mod-2", "mod-2", "Moderator");

        var response = await client.PostAsJsonAsync("/api/moderation/users/target-2/suspend", new SuspendUserRequestDto
        {
            SuspendedUntilUtc = DateTime.UtcNow.AddDays(1),
            Reason = "not permitted"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await factory.ExecuteDbContextAsync(async db =>
        {
            (await db.ModerationAuditLogs.AnyAsync()).Should().BeFalse();
            var user = await db.Users.SingleAsync(u => u.Id == "target-2");
            user.SuspendedUntilUtc.Should().BeNull();
        });
    }

    [Fact]
    public async Task GetAudit_WhenModeratorAuthenticated_ReturnsPagedResult()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "mod-3", "mod-3");
        await factory.ExecuteDbContextAsync(async db =>
        {
            db.ModerationAuditLogs.Add(new ModerationAuditLog
            {
                CreatedUtc = DateTime.UtcNow,
                ActorUserId = "mod-3",
                ActorRole = "Moderator",
                ActionType = "PostHide",
                TargetType = "Post",
                TargetId = "100"
            });
            db.ModerationAuditLogs.Add(new ModerationAuditLog
            {
                CreatedUtc = DateTime.UtcNow,
                ActorUserId = "mod-3",
                ActorRole = "Moderator",
                ActionType = "CommentHide",
                TargetType = "Comment",
                TargetId = "200"
            });
            await db.SaveChangesAsync();
        });

        using var client = factory.CreateAuthenticatedClient("mod-3", "mod-3", "Moderator");

        var response = await client.GetAsync("/api/moderation/audit?page=1&pageSize=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<ModerationAuditDto>>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().Be(2);
        body.PageNumber.Should().Be(1);
        body.PageSize.Should().Be(1);
        body.Items.Should().HaveCount(1);
    }

    private static Task SeedUserAsync(IntegrationTestApplicationFactory factory, string userId, string userName)
    {
        return factory.ExecuteDbContextAsync(async db =>
        {
            await db.Users.AddAsync(new ApplicationUser
            {
                Id = userId,
                UserName = userName,
                Email = $"{userName}@example.test",
                EmailConfirmed = true
            });
            await db.SaveChangesAsync();
        });
    }

    private static Task SeedPostAsync(IntegrationTestApplicationFactory factory, int postId, string authorId)
    {
        return factory.ExecuteDbContextAsync(async db =>
        {
            await db.Posts.AddAsync(new Post
            {
                Id = postId,
                Title = $"Post {postId}",
                Content = "Body",
                CreatedById = authorId,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                IsDeleted = false
            });
            await db.SaveChangesAsync();
        });
    }

    private static Task SeedCommentAsync(IntegrationTestApplicationFactory factory, int commentId, int postId, string authorId, bool isDeleted)
    {
        return factory.ExecuteDbContextAsync(async db =>
        {
            await db.Comments.AddAsync(new Comment
            {
                Id = commentId,
                PostId = postId,
                AuthorId = authorId,
                Text = "Comment",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                IsDeleted = isDeleted
            });
            await db.SaveChangesAsync();
        });
    }
}
