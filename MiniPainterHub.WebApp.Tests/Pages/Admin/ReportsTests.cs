using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages.Admin;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages.Admin;

public class ReportsTests : TestContext
{
    [Fact]
    public void LoadsQueueAndResolveActionCallsService()
    {
        long? resolvedReportId = null;
        ResolveReportRequestDto? resolvedRequest = null;

        this.AddReportStub(new StubReportService
        {
            GetQueueHandler = query => Task.FromResult(new MiniPainterHub.WebApp.Services.Http.ApiResult<PagedResult<ReportQueueItemDto>?>(true, HttpStatusCode.OK, new PagedResult<ReportQueueItemDto>
            {
                Items = new[]
                {
                    new ReportQueueItemDto
                    {
                        Id = 5,
                        CreatedUtc = DateTime.UtcNow,
                        Status = ReportStatuses.Open,
                        TargetType = ReportTargetTypes.Post,
                        TargetId = "10",
                        ReasonCode = ReportReasonCodes.Spam,
                        ReporterUserId = "viewer",
                        ReporterUserName = "Viewer",
                        TargetSummary = "Target post",
                        TargetUrl = "/posts/10"
                    }
                },
                PageNumber = query.Page,
                PageSize = query.PageSize,
                TotalCount = 1
            })),
            ResolveHandler = (reportId, request) =>
            {
                resolvedReportId = reportId;
                resolvedRequest = request;
                return Task.FromResult(true);
            }
        });
        this.AddModerationStub();

        var cut = RenderComponent<Reports>();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='reports-row']").Should().HaveCount(1);
            cut.Find("[data-testid='reports-filter-target-type']").TextContent.Should().Contain("Hobby project");
        });

        cut.Find("[data-testid='report-resolution-note']").Change("Needs moderator review");
        cut.Find("[data-testid='report-resolve-reviewed']").Click();

        cut.WaitForAssertion(() =>
        {
            resolvedReportId.Should().Be(5);
            resolvedRequest.Should().NotBeNull();
            resolvedRequest!.Status.Should().Be(ReportStatuses.Reviewed);
            resolvedRequest.ResolutionNote.Should().Be("Needs moderator review");
        });
    }

    [Fact]
    public void HobbyProjectInspector_LoadsHiddenStaffPreview_AndClosesWithoutNavigatingToPublicTarget()
    {
        int? previewProjectId = null;
        this.AddReportStub(CreateProjectReportStub(projectId: 17));
        this.AddModerationStub(new StubModerationService
        {
            GetProjectPreviewHandler = projectId =>
            {
                previewProjectId = projectId;
                return Task.FromResult(new MiniPainterHub.WebApp.Services.Http.ApiResult<ModerationHobbyProjectPreviewDto?>(
                    true,
                    HttpStatusCode.OK,
                    new ModerationHobbyProjectPreviewDto
                    {
                        ProjectId = projectId,
                        Title = "Hidden winter company",
                        DescriptionSnippet = "Snow bases and weathered green armor.",
                        OwnerUserId = "project-owner",
                        Kind = HobbyProjectKinds.Army,
                        Status = HobbyProjectStatuses.Completed,
                        IsHidden = true,
                        CreatedUtc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                        UpdatedUtc = new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc),
                        ModerationReason = "Unsafe project metadata"
                    }));
            }
        });

        var cut = RenderComponent<Reports>();
        cut.WaitForElement("[data-testid='report-project-inspect']").Click();

        cut.WaitForAssertion(() =>
        {
            previewProjectId.Should().Be(17);
            cut.Find("[data-testid='report-project-inspector']").GetAttribute("aria-label").Should().Be("Hobby project inspector");
            cut.Find("[data-testid='report-project-preview-title']").TextContent.Should().Be("Hidden winter company");
            cut.Find("[data-testid='report-project-preview-description']").TextContent.Should().Contain("Snow bases");
            cut.Find("[data-testid='report-project-preview-meta']").TextContent.Should().ContainAll("project-owner", "Army", "Completed", "Updated");
            cut.Find("[data-testid='report-project-preview-state']").TextContent.Should().Contain("Hidden by staff");
            cut.Find("[data-testid='report-project-preview-moderation-reason']").TextContent.Should().Contain("Unsafe project metadata");
            cut.Find("[data-testid='report-project-restore']").Should().NotBeNull();
            cut.Find("a[href='/projects/17']").Should().NotBeNull("the row may still expose the public URL, but the inspector loads through the staff API");
        });

        cut.Find("[data-testid='report-project-inspector-close']").Click();
        cut.FindAll("[data-testid='report-project-inspector']").Should().BeEmpty();
        cut.Find("[data-testid='report-project-inspect']").GetAttribute("aria-expanded").Should().Be("false");
    }

    [Fact]
    public void HobbyProjectInspector_RequiresReason_ThenHidesAndRestoresWithRefreshedPreview()
    {
        var hidden = false;
        string? moderationReason = null;
        var previewCalls = 0;
        var hideCalls = 0;
        var restoreCalls = 0;
        this.AddReportStub(CreateProjectReportStub(projectId: 23));
        this.AddModerationStub(new StubModerationService
        {
            GetProjectPreviewHandler = projectId =>
            {
                previewCalls++;
                return Task.FromResult(new MiniPainterHub.WebApp.Services.Http.ApiResult<ModerationHobbyProjectPreviewDto?>(
                    true,
                    HttpStatusCode.OK,
                    new ModerationHobbyProjectPreviewDto
                    {
                        ProjectId = projectId,
                        Title = "Rusted cohort",
                        DescriptionSnippet = "Copper weathering diary.",
                        OwnerUserId = "owner-23",
                        Kind = HobbyProjectKinds.Army,
                        Status = HobbyProjectStatuses.InProgress,
                        IsHidden = hidden,
                        CreatedUtc = DateTime.UtcNow.AddDays(-10),
                        UpdatedUtc = DateTime.UtcNow,
                        ModerationReason = moderationReason
                    }));
            },
            HideProjectHandler = (projectId, request) =>
            {
                projectId.Should().Be(23);
                hideCalls++;
                hidden = true;
                moderationReason = request.Reason;
                return Task.FromResult(true);
            },
            RestoreProjectHandler = (projectId, request) =>
            {
                projectId.Should().Be(23);
                restoreCalls++;
                hidden = false;
                moderationReason = request.Reason;
                return Task.FromResult(true);
            }
        });

        var cut = RenderComponent<Reports>();
        cut.WaitForElement("[data-testid='report-project-inspect']").Click();
        cut.WaitForElement("[data-testid='report-project-hide']").Click();

        cut.WaitForAssertion(() =>
        {
            hideCalls.Should().Be(0);
            cut.Find("[data-testid='report-project-action-error']").TextContent.Should().Contain("Enter a reason");
            cut.Find("[data-testid='report-project-live']").TextContent.Should().Contain("Enter a reason");
        });

        cut.Find("[data-testid='report-project-action-reason']").Change("Reported title violates safety policy");
        cut.Find("[data-testid='report-project-hide']").Click();
        cut.WaitForAssertion(() =>
        {
            hideCalls.Should().Be(1);
            previewCalls.Should().Be(2);
            cut.Find("[data-testid='report-project-preview-state']").TextContent.Should().Contain("Hidden by staff");
            cut.Find("[data-testid='report-project-preview-moderation-reason']").TextContent.Should().Contain("Reported title violates safety policy");
            cut.Find("[data-testid='report-project-action-success']").TextContent.Should().Contain("Project 23 hidden");
            cut.Find("[data-testid='report-project-live']").TextContent.Should().Contain("Project 23 hidden");
        });

        cut.Find("[data-testid='report-project-action-reason']").Change("Owner corrected the project metadata");
        cut.Find("[data-testid='report-project-restore']").Click();
        cut.WaitForAssertion(() =>
        {
            restoreCalls.Should().Be(1);
            previewCalls.Should().Be(3);
            cut.Find("[data-testid='report-project-preview-state']").TextContent.Should().Contain("Not hidden by staff");
            cut.Find("[data-testid='report-project-preview-moderation-reason']").TextContent.Should().Contain("Owner corrected the project metadata");
            cut.Find("[data-testid='report-project-action-success']").TextContent.Should().Contain("Project 23 restored");
        });
    }

    private static StubReportService CreateProjectReportStub(int projectId) =>
        new()
        {
            GetQueueHandler = query => Task.FromResult(new MiniPainterHub.WebApp.Services.Http.ApiResult<PagedResult<ReportQueueItemDto>?>(
                true,
                HttpStatusCode.OK,
                new PagedResult<ReportQueueItemDto>
                {
                    Items = new[]
                    {
                        new ReportQueueItemDto
                        {
                            Id = 90 + projectId,
                            CreatedUtc = DateTime.UtcNow,
                            Status = ReportStatuses.Open,
                            TargetType = ReportTargetTypes.HobbyProject,
                            TargetId = projectId.ToString(),
                            ReasonCode = ReportReasonCodes.Other,
                            ReporterUserId = "reporter",
                            ReporterUserName = "Reporter",
                            TargetSummary = $"Project {projectId}",
                            TargetUrl = $"/projects/{projectId}"
                        }
                    },
                    PageNumber = query.Page,
                    PageSize = query.PageSize,
                    TotalCount = 1
                }))
        };
}
