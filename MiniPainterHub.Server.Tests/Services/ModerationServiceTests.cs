using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Tests.Infrastructure;
using System.Threading.Tasks;
using System;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class ModerationServiceTests
{
    [Fact]
    public async Task HideAndUnhide_Post_UpdatesStatus_AndWritesAudit()
    {
        using var db = AppDbContextFactory.Create();
        db.Posts.Add(new Post { Id = 1, Title = "t", Content = "c", CreatedById = "u1" });
        await db.SaveChangesAsync();

        var audit = new AuditLogService(db);
        var sut = new ModerationService(db, audit);

        await sut.HideAsync("admin", "post", 1, "reason");
        (await db.Posts.IgnoreQueryFilters().SingleAsync(x => x.Id == 1)).Status.Should().Be(ContentStatus.Hidden);

        await sut.UnhideAsync("admin", "post", 1, "undo");
        (await db.Posts.IgnoreQueryFilters().SingleAsync(x => x.Id == 1)).Status.Should().Be(ContentStatus.Active);

        db.ModerationActions.Should().HaveCount(2);
    }

    [Fact]
    public async Task UserRestriction_Expires_ByUntil()
    {
        using var db = AppDbContextFactory.Create();
        db.UserRestrictions.Add(new UserRestriction
        {
            UserId = "u1",
            CanComment = false,
            CanPost = false,
            CanPostImages = false,
            IsSuspended = true,
            Until = DateTime.UtcNow.AddMinutes(-5)
        });
        await db.SaveChangesAsync();

        var sut = new UserModerationService(db, new AuditLogService(db));
        var result = await sut.GetOrDefaultAsync("u1");

        result.IsSuspended.Should().BeFalse();
        result.CanPost.Should().BeTrue();
        result.CanComment.Should().BeTrue();
        result.CanPostImages.Should().BeTrue();
    }
}
