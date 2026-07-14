using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Tests.Infrastructure;
using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public sealed class HobbyProjectCrossCuttingControllerTests
{
    [Fact]
    public async Task ReportProject_WhenVisibleProjectHasProgress_PersistsAndMapsQueueTarget()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        var projectId = await SeedProjectAsync(factory, "project-owner", "Rusted Sentinel Cohort");
        await SeedUserAsync(factory, "project-reporter", "project-reporter");
        using var reporterClient = factory.CreateAuthenticatedClient("project-reporter", "project-reporter");

        var reportResponse = await reporterClient.PostAsJsonAsync($"/api/reports/projects/{projectId}", new CreateReportRequestDto
        {
            ReasonCode = ReportReasonCodes.Spam
        });

        reportResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var moderatorClient = factory.CreateAuthenticatedClient("project-moderator", "project-moderator", "Moderator");
        var queueResponse = await moderatorClient.GetAsync("/api/reports?targetType=HobbyProject&page=1&pageSize=10");
        queueResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await queueResponse.Content.ReadFromJsonAsync<PagedResult<ReportQueueItemDto>>();
        var item = queue!.Items.Should().ContainSingle().Subject;
        item.TargetType.Should().Be(ReportTargetTypes.HobbyProject);
        item.TargetSummary.Should().Be("Rusted Sentinel Cohort");
        item.TargetUrl.Should().Be($"/projects/{projectId}");
    }

    [Fact]
    public async Task ReportProject_WhenProjectHasNoVisibleProgress_ReturnsNotFound()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        var projectId = await SeedProjectAsync(factory, "empty-owner", "Empty project", includePost: false);
        await SeedUserAsync(factory, "empty-reporter", "empty-reporter");
        using var client = factory.CreateAuthenticatedClient("empty-reporter", "empty-reporter");

        var response = await client.PostAsJsonAsync($"/api/reports/projects/{projectId}", new CreateReportRequestDto
        {
            ReasonCode = ReportReasonCodes.Spam
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ProjectModeration_WhenModeratorHidesAndRestores_UpdatesPreviewAndAudit()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        var projectId = await SeedProjectAsync(factory, "moderated-owner", "Moderated project");
        await SeedUserAsync(factory, "project-mod", "project-mod");
        using var client = factory.CreateAuthenticatedClient("project-mod", "project-mod", "Moderator");

        var hideResponse = await client.PostAsJsonAsync($"/api/moderation/projects/{projectId}/hide", new ModerationActionRequestDto
        {
            Reason = "unsafe metadata"
        });
        hideResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var previewResponse = await client.GetAsync($"/api/moderation/projects/{projectId}/preview");
        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = await previewResponse.Content.ReadFromJsonAsync<ModerationHobbyProjectPreviewDto>();
        preview!.IsHidden.Should().BeTrue();
        preview.ModeratedByUserId.Should().Be("project-mod");

        var restoreResponse = await client.PostAsJsonAsync($"/api/moderation/projects/{projectId}/restore", new ModerationActionRequestDto
        {
            Reason = "appeal accepted"
        });
        restoreResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await factory.ExecuteDbContextAsync(async db =>
        {
            var project = await db.HobbyProjects.SingleAsync(candidate => candidate.Id == projectId);
            project.IsHidden.Should().BeFalse();
            var audit = await db.ModerationAuditLogs.OrderBy(row => row.Id).ToListAsync();
            audit.Select(row => row.ActionType).Should().Equal("HobbyProjectHide", "HobbyProjectRestore");
            audit.Should().OnlyContain(row => row.TargetType == ReportTargetTypes.HobbyProject);
        });
    }

    [Fact]
    public async Task HideProject_WhenAuthenticatedUserIsNotStaff_ReturnsForbidden()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        var projectId = await SeedProjectAsync(factory, "role-owner", "Role protected project");
        using var client = factory.CreateAuthenticatedClient("ordinary-user", "ordinary-user", "User");

        var response = await client.PostAsJsonAsync($"/api/moderation/projects/{projectId}/hide", new ModerationActionRequestDto
        {
            Reason = "not permitted"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SearchProjects_WhenAnonymous_ReturnsOnlyMatchingPublicProjects()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        var visibleId = await SeedProjectAsync(factory, "search-owner", "Winter Cohort");
        await SeedProjectAsync(factory, "hidden-search-owner", "Winter Hidden", isHidden: true);
        await SeedProjectAsync(factory, "empty-search-owner", "Winter Empty", includePost: false);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/search/projects?q=winter&page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<HobbyProjectSummaryDto>>();
        result!.Items.Should().ContainSingle();
        result.Items.Single().Id.Should().Be(visibleId);
        result.Items.Single().IsPublic.Should().BeTrue();
    }

    private static Task SeedUserAsync(
        IntegrationTestApplicationFactory factory,
        string userId,
        string userName) =>
        factory.ExecuteDbContextAsync(async db =>
        {
            if (!await db.Users.AnyAsync(user => user.Id == userId))
            {
                await db.Users.AddAsync(new ApplicationUser
                {
                    Id = userId,
                    UserName = userName,
                    Email = $"{userName}@example.test",
                    EmailConfirmed = true
                });
                await db.SaveChangesAsync();
            }
        });

    private static async Task<int> SeedProjectAsync(
        IntegrationTestApplicationFactory factory,
        string ownerUserId,
        string title,
        bool includePost = true,
        bool isHidden = false)
    {
        var projectId = 0;
        await factory.ExecuteDbContextAsync(async db =>
        {
            var owner = new ApplicationUser
            {
                Id = ownerUserId,
                UserName = ownerUserId,
                Email = $"{ownerUserId}@example.test",
                EmailConfirmed = true,
                Profile = new Profile
                {
                    UserId = ownerUserId,
                    DisplayName = ownerUserId
                }
            };
            db.Users.Add(owner);

            Post? post = null;
            if (includePost)
            {
                post = new Post
                {
                    Title = $"{title} progress",
                    Content = "Visible progress post",
                    CreatedById = ownerUserId,
                    CreatedBy = owner,
                    CreatedUtc = DateTime.UtcNow.AddDays(-1),
                    UpdatedUtc = DateTime.UtcNow.AddDays(-1)
                };
                db.Posts.Add(post);
                await db.SaveChangesAsync();
            }

            var project = new HobbyProject
            {
                OwnerUserId = ownerUserId,
                OwnerUser = owner,
                Title = title,
                Description = "A searchable hobby project.",
                Kind = HobbyProjectKinds.Army,
                Status = HobbyProjectStatuses.InProgress,
                CreatedUtc = DateTime.UtcNow.AddDays(-2),
                UpdatedUtc = DateTime.UtcNow,
                IsHidden = isHidden
            };
            if (post is not null)
            {
                project.Entries.Add(new HobbyProjectEntry
                {
                    Project = project,
                    Post = post,
                    PostId = post.Id,
                    LinkedUtc = post.CreatedUtc,
                    ShowcaseOrder = 1
                });
            }

            db.HobbyProjects.Add(project);
            await db.SaveChangesAsync();
            projectId = project.Id;
        });

        return projectId;
    }
}
