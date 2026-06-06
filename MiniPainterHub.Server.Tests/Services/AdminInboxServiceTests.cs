using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Tests.Infrastructure;
using Moq;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class AdminInboxServiceTests
{
    [Fact]
    public async Task GetInboxAsync_FiltersByReportedPostSearchAndType()
    {
        await using var context = AppDbContextFactory.Create();
        await context.Users.AddAsync(TestData.CreateUser("author-1", "PainterA"));
        await context.Posts.AddAsync(TestData.CreatePost(10, "author-1"));
        await context.ContentReports.AddAsync(new ContentReport
        {
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            ReporterUserId = "reporter-1",
            TargetType = ReportTargetTypes.Post,
            TargetId = "10",
            ReasonCode = ReportReasonCodes.Spam,
            Status = ReportStatuses.Open
        });
        await context.SaveChangesAsync();
        var service = new AdminInboxService(context, CreateUserManagerMock().Object);

        var result = await service.GetInboxAsync(new AdminInboxQueryDto
        {
            ItemType = AdminInboxItemTypes.Post,
            State = AdminInboxStates.Reported,
            Search = "Title 10",
            WindowHours = 24,
            Page = 1,
            PageSize = 25
        });

        result.Items.Should().ContainSingle();
        var item = result.Items.Single();
        item.TargetType.Should().Be(ReportTargetTypes.Post);
        item.TargetId.Should().Be("10");
        item.OpenReportCount.Should().Be(1);
        item.State.Should().Be(AdminInboxStates.Reported);
    }

    [Fact]
    public async Task ReviewAsync_ResolvesOpenReportsAndWritesAudit()
    {
        await using var context = AppDbContextFactory.Create();
        await context.Users.AddAsync(TestData.CreateUser("actor-1", "moderator"));
        await context.Users.AddAsync(TestData.CreateUser("author-1", "PainterA"));
        await context.Posts.AddAsync(TestData.CreatePost(10, "author-1"));
        await context.ContentReports.AddAsync(new ContentReport
        {
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            ReporterUserId = "reporter-1",
            TargetType = ReportTargetTypes.Post,
            TargetId = "10",
            ReasonCode = ReportReasonCodes.Spam,
            Status = ReportStatuses.Open
        });
        await context.SaveChangesAsync();

        var userManager = CreateUserManagerMock();
        userManager.Setup(m => m.FindByIdAsync("actor-1")).ReturnsAsync(TestData.CreateUser("actor-1", "moderator"));
        userManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(["Moderator"]);
        var service = new AdminInboxService(context, userManager.Object);

        await service.ReviewAsync(ReportTargetTypes.Post, "10", "actor-1", new AdminInboxReviewRequestDto
        {
            Reason = "Checked in inbox"
        });

        var report = await context.ContentReports.SingleAsync();
        report.Status.Should().Be(ReportStatuses.Reviewed);
        report.ReviewedByUserId.Should().Be("actor-1");
        report.ResolutionNote.Should().Be("Checked in inbox");

        var audit = await context.ModerationAuditLogs.SingleAsync();
        audit.ActionType.Should().Be("PostReviewed");
        audit.TargetType.Should().Be(ReportTargetTypes.Post);
        audit.TargetId.Should().Be("10");
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
