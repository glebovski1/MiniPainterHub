using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public class ReportsControllerTests
{
    [Fact]
    public async Task ReportPost_WhenAuthenticated_PersistsOpenReport()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "reporter", "reporter");
        await SeedUserAsync(factory, "author", "author");
        await SeedPostAsync(factory, 10, "author", "Target", "Target post");
        using var client = factory.CreateAuthenticatedClient("reporter", "reporter");

        var response = await client.PostAsJsonAsync("/api/reports/posts/10", new CreateReportRequestDto
        {
            ReasonCode = ReportReasonCodes.Spam
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await factory.ExecuteDbContextAsync(async db =>
        {
            var report = db.ContentReports.Single();
            report.TargetType.Should().Be(ReportTargetTypes.Post);
            report.TargetId.Should().Be("10");
            report.Status.Should().Be(ReportStatuses.Open);
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ReportPost_WhenDuplicateOpenReportExists_ReturnsValidationProblemDetails()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "reporter", "reporter");
        await SeedUserAsync(factory, "author", "author");
        await SeedPostAsync(factory, 11, "author", "Target", "Target post");
        using var client = factory.CreateAuthenticatedClient("reporter", "reporter");

        await client.PostAsJsonAsync("/api/reports/posts/11", new CreateReportRequestDto
        {
            ReasonCode = ReportReasonCodes.Spam
        });

        var duplicate = await client.PostAsJsonAsync("/api/reports/posts/11", new CreateReportRequestDto
        {
            ReasonCode = ReportReasonCodes.Spam
        });

        await ProblemDetailsAssertions.AssertAsync(
            duplicate,
            HttpStatusCode.BadRequest,
            "Validation error",
            expectedErrorKeys: new[] { "report" });
    }

    [Fact]
    public async Task QueueAndResolve_WhenModerator_ReturnsReportsAndUpdatesStatus()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "reporter", "reporter", "Viewer");
        await SeedUserAsync(factory, "author", "author", "Artist");
        await SeedPostAsync(factory, 12, "author", "Queue target", "Queued target");
        await SeedReportAsync(factory, "reporter", ReportTargetTypes.Post, "12");
        using var client = factory.CreateAuthenticatedClient("moderator", "moderator", "Moderator");

        var queueResponse = await client.GetAsync("/api/reports?page=1&pageSize=10&status=Open");

        queueResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await queueResponse.Content.ReadFromJsonAsync<PagedResult<ReportQueueItemDto>>();
        queue.Should().NotBeNull();
        queue!.Items.Should().ContainSingle();
        queue.Items.Single().TargetSummary.Should().Be("Queue target");

        var resolveResponse = await client.PostAsJsonAsync("/api/reports/1/resolve", new ResolveReportRequestDto
        {
            Status = ReportStatuses.Reviewed,
            ResolutionNote = "Checked by moderator."
        });

        resolveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await factory.ExecuteDbContextAsync(async db =>
        {
            var report = db.ContentReports.Single();
            report.Status.Should().Be(ReportStatuses.Reviewed);
            report.ResolutionNote.Should().Be("Checked by moderator.");
            report.ReviewedByUserId.Should().Be("moderator");
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task GetQueue_WhenPaginationIsInvalid_ReturnsValidationProblemDetails()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient("moderator", "moderator", "Moderator");

        var response = await client.GetAsync("/api/reports?page=0&pageSize=-1");

        await ProblemDetailsAssertions.AssertAsync(
            response,
            HttpStatusCode.BadRequest,
            "Validation error",
            expectedErrorKeys: new[] { "page", "pageSize" });
    }

    [Fact]
    public async Task Resolve_WhenReportAlreadyResolved_ReturnsValidationProblemDetails()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "reporter", "reporter", "Viewer");
        await SeedReportAsync(factory, "reporter", ReportTargetTypes.User, "target-user", ReportStatuses.Reviewed);
        using var client = factory.CreateAuthenticatedClient("moderator", "moderator", "Moderator");

        var response = await client.PostAsJsonAsync("/api/reports/1/resolve", new ResolveReportRequestDto
        {
            Status = ReportStatuses.Actioned,
            ResolutionNote = "Should not overwrite."
        });

        await ProblemDetailsAssertions.AssertAsync(
            response,
            HttpStatusCode.BadRequest,
            "Validation error",
            expectedErrorKeys: new[] { "status" });

        await factory.ExecuteDbContextAsync(async db =>
        {
            var report = db.ContentReports.Single();
            report.Status.Should().Be(ReportStatuses.Reviewed);
            report.ReviewedByUserId.Should().BeNull();
            report.ResolutionNote.Should().BeNull();
            await Task.CompletedTask;
        });
    }

    private static Task SeedUserAsync(
        IntegrationTestApplicationFactory factory,
        string userId,
        string userName,
        string? displayName = null)
    {
        return factory.ExecuteDbContextAsync(async db =>
        {
            await db.Users.AddAsync(new ApplicationUser
            {
                Id = userId,
                UserName = userName,
                Email = $"{userName}@example.test",
                Profile = new Profile
                {
                    UserId = userId,
                    DisplayName = displayName ?? userName
                }
            });

            await db.SaveChangesAsync();
        });
    }

    private static Task SeedPostAsync(
        IntegrationTestApplicationFactory factory,
        int postId,
        string userId,
        string title,
        string content)
    {
        return factory.ExecuteDbContextAsync(async db =>
        {
            await db.Posts.AddAsync(new Post
            {
                Id = postId,
                Title = title,
                Content = content,
                CreatedById = userId,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        });
    }

    private static Task SeedReportAsync(
        IntegrationTestApplicationFactory factory,
        string reporterUserId,
        string targetType,
        string targetId,
        string status = ReportStatuses.Open)
    {
        return factory.ExecuteDbContextAsync(async db =>
        {
            await db.ContentReports.AddAsync(new ContentReport
            {
                Id = 1,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                ReporterUserId = reporterUserId,
                TargetType = targetType,
                TargetId = targetId,
                ReasonCode = ReportReasonCodes.Spam,
                Status = status
            });

            await db.SaveChangesAsync();
        });
    }
}
