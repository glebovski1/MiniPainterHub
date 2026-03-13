using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class ReportServiceTests
{
    [Fact]
    public async Task SubmitCommentReportAsync_WhenOtherReasonLacksDetails_ThrowsValidationException()
    {
        await using var context = AppDbContextFactory.Create();
        var service = new ReportService(context);

        var act = async () => await service.SubmitCommentReportAsync("reporter-1", 7, new CreateReportRequestDto
        {
            ReasonCode = ReportReasonCodes.Other
        });

        var exception = await act.Should().ThrowAsync<DomainValidationException>();
        exception.Which.Errors.Should().ContainKey("details");
    }

    [Fact]
    public async Task SubmitPostReportAsync_WhenReportingOwnPost_ThrowsForbiddenException()
    {
        await using var context = AppDbContextFactory.Create();
        var reporter = CreateUserWithProfile("reporter-1", "reporter", "Reporter");
        var post = CreatePost(11, reporter, "Own post", "Cannot self-report");
        context.Users.Add(reporter);
        context.Profiles.Add(reporter.Profile!);
        context.Posts.Add(post);
        await context.SaveChangesAsync();
        var service = new ReportService(context);

        var act = async () => await service.SubmitPostReportAsync(reporter.Id, post.Id, new CreateReportRequestDto
        {
            ReasonCode = ReportReasonCodes.Spam
        });

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You cannot report your own post.");
    }

    [Fact]
    public async Task SubmitUserReportAsync_WhenDuplicateOpenReportExists_ThrowsValidationException()
    {
        await using var context = AppDbContextFactory.Create();
        var reporter = CreateUserWithProfile("reporter-1", "reporter", "Reporter");
        var target = CreateUserWithProfile("target-1", "target", "Target User");
        context.Users.AddRange(reporter, target);
        context.Profiles.AddRange(reporter.Profile!, target.Profile!);
        context.ContentReports.Add(new ContentReport
        {
            Id = 99,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            ReporterUserId = reporter.Id,
            TargetType = ReportTargetTypes.User,
            TargetId = target.Id,
            ReasonCode = ReportReasonCodes.Spam,
            Status = ReportStatuses.Open
        });
        await context.SaveChangesAsync();
        var service = new ReportService(context);

        var act = async () => await service.SubmitUserReportAsync(reporter.Id, target.Id, new CreateReportRequestDto
        {
            ReasonCode = ReportReasonCodes.Spam
        });

        var exception = await act.Should().ThrowAsync<DomainValidationException>();
        exception.Which.Errors.Should().ContainKey("report");
    }

    [Fact]
    public async Task GetQueueAsync_MapsReporterReviewerAndTargetSummaries()
    {
        await using var context = AppDbContextFactory.Create();
        var reporter = CreateUserWithProfile("reporter-1", "reporter", "Reporter Name");
        var reviewer = CreateUserWithProfile("reviewer-1", "moderator", "Moderator Name");
        var author = CreateUserWithProfile("author-1", "author", "Author Name");
        var post = CreatePost(21, author, "Weathering breakdown", "Pigments and oils");
        var comment = TestData.CreateComment(31, post.Id, author.Id);
        comment.Text = new string('x', 140);
        comment.Author = author;
        comment.Post = post;
        post.Comments.Add(comment);

        context.Users.AddRange(reporter, reviewer, author);
        context.Profiles.AddRange(reporter.Profile!, reviewer.Profile!, author.Profile!);
        context.Posts.Add(post);
        context.Comments.Add(comment);
        context.ContentReports.AddRange(
            new ContentReport
            {
                Id = 1,
                CreatedUtc = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc),
                UpdatedUtc = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc),
                ReporterUserId = reporter.Id,
                TargetType = ReportTargetTypes.Post,
                TargetId = post.Id.ToString(),
                ReasonCode = ReportReasonCodes.Spam,
                Status = ReportStatuses.Open
            },
            new ContentReport
            {
                Id = 2,
                CreatedUtc = new DateTime(2026, 3, 11, 12, 0, 0, DateTimeKind.Utc),
                UpdatedUtc = new DateTime(2026, 3, 11, 12, 5, 0, DateTimeKind.Utc),
                ReporterUserId = reporter.Id,
                TargetType = ReportTargetTypes.Comment,
                TargetId = comment.Id.ToString(),
                ReasonCode = ReportReasonCodes.Other,
                Details = "Escalated",
                Status = ReportStatuses.Reviewed,
                ReviewedByUserId = reviewer.Id,
                ReviewedUtc = new DateTime(2026, 3, 11, 12, 5, 0, DateTimeKind.Utc),
                ResolutionNote = "Handled"
            });
        await context.SaveChangesAsync();
        var service = new ReportService(context);

        var queue = await service.GetQueueAsync(new ReportQueueQueryDto { Page = 1, PageSize = 10 });
        var items = queue.Items.ToList();

        items.Should().HaveCount(2);
        items[0].Id.Should().Be(2);
        items[0].ReporterUserName.Should().Be("Reporter Name");
        items[0].ReviewedByUserName.Should().Be("Moderator Name");
        items[0].TargetUrl.Should().Be($"/posts/{post.Id}");
        items[0].TargetSummary.Should().HaveLength(123).And.EndWith("...");
        items[1].TargetSummary.Should().Be("Weathering breakdown");
        items[1].TargetUrl.Should().Be($"/posts/{post.Id}");
    }

    [Fact]
    public async Task ResolveAsync_WhenReportIsOpen_UpdatesReviewerStatusAndNote()
    {
        await using var context = AppDbContextFactory.Create();
        context.ContentReports.Add(new ContentReport
        {
            Id = 7,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            ReporterUserId = "reporter-1",
            TargetType = ReportTargetTypes.User,
            TargetId = "target-1",
            ReasonCode = ReportReasonCodes.Spam,
            Status = ReportStatuses.Open
        });
        await context.SaveChangesAsync();
        var service = new ReportService(context);

        await service.ResolveAsync("reviewer-1", 7, new ResolveReportRequestDto
        {
            Status = ReportStatuses.Actioned,
            ResolutionNote = "User suspended."
        });

        var stored = await context.ContentReports.FindAsync(7L);
        stored.Should().NotBeNull();
        stored!.Status.Should().Be(ReportStatuses.Actioned);
        stored.ReviewedByUserId.Should().Be("reviewer-1");
        stored.ResolutionNote.Should().Be("User suspended.");
        stored.ReviewedUtc.Should().NotBeNull();
        stored.UpdatedUtc.Should().Be(stored.ReviewedUtc);
    }

    private static ApplicationUser CreateUserWithProfile(string id, string userName, string displayName)
    {
        var user = TestData.CreateUser(id, userName);
        var profile = TestData.CreateProfile(id, displayName, string.Empty);
        profile.User = user;
        user.Profile = profile;
        return user;
    }

    private static Post CreatePost(int id, ApplicationUser author, string title, string content)
    {
        var post = TestData.CreatePost(id, author.Id);
        post.Title = title;
        post.Content = content;
        post.CreatedBy = author;
        return post;
    }
}
