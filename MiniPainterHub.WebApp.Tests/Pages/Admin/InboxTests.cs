using System;
using System.Net;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages.Admin;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages.Admin;

public class InboxTests : TestContext
{
    [Fact]
    public async Task KeepsInspectorOnNewestSelectionWhenOlderDetailFinishesLast()
    {
        var staleFirstDetail = new TaskCompletionSource<ApiResult<AdminInboxDetailDto?>>();
        var firstDetailCalls = 0;
        this.AddAdminStub(new StubAdminService
        {
            GetInboxHandler = query => Task.FromResult(new ApiResult<PagedResult<AdminInboxItemDto>?>(true, HttpStatusCode.OK, new PagedResult<AdminInboxItemDto>
            {
                Items = new[]
                {
                    InboxItem("1", "First post"),
                    InboxItem("2", "Second post")
                },
                PageNumber = query.Page,
                PageSize = query.PageSize,
                TotalCount = 2
            })),
            GetInboxDetailHandler = (_, targetId) =>
            {
                if (targetId == "1")
                {
                    firstDetailCalls++;
                    return firstDetailCalls == 1
                        ? Task.FromResult(new ApiResult<AdminInboxDetailDto?>(true, HttpStatusCode.OK, InboxDetail("1", "Initial first detail")))
                        : staleFirstDetail.Task;
                }

                return Task.FromResult(new ApiResult<AdminInboxDetailDto?>(true, HttpStatusCode.OK, InboxDetail("2", "Newest second detail")));
            }
        });
        this.AddModerationStub();

        var cut = RenderWithAuth<Inbox>();

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='admin-inbox-inspector']").TextContent.Should().Contain("Initial first detail"));

        var firstSelection = cut.FindAll("[data-testid='admin-inbox-select']")[0]
            .TriggerEventAsync("onclick", new MouseEventArgs());
        await Task.Delay(25);
        await cut.FindAll("[data-testid='admin-inbox-select']")[1]
            .TriggerEventAsync("onclick", new MouseEventArgs());

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='admin-inbox-inspector']").TextContent.Should().Contain("Newest second detail"));

        staleFirstDetail.SetResult(new ApiResult<AdminInboxDetailDto?>(true, HttpStatusCode.OK, InboxDetail("1", "Stale first detail")));
        await firstSelection;

        cut.WaitForAssertion(() =>
        {
            var inspector = cut.Find("[data-testid='admin-inbox-inspector']").TextContent;
            inspector.Should().Contain("Newest second detail");
            inspector.Should().NotContain("Stale first detail");
        });
    }

    [Fact]
    public void ClearsInspectorWhenInboxReloadFails()
    {
        var calls = 0;
        this.AddAdminStub(new StubAdminService
        {
            GetInboxHandler = query =>
            {
                calls++;
                return calls == 1
                    ? Task.FromResult(new ApiResult<PagedResult<AdminInboxItemDto>?>(true, HttpStatusCode.OK, new PagedResult<AdminInboxItemDto>
                    {
                        Items = new[] { InboxItem("1", "First post") },
                        PageNumber = query.Page,
                        PageSize = query.PageSize,
                        TotalCount = 1
                    }))
                    : Task.FromResult(new ApiResult<PagedResult<AdminInboxItemDto>?>(false, HttpStatusCode.InternalServerError, null));
            },
            GetInboxDetailHandler = (_, targetId) => Task.FromResult(new ApiResult<AdminInboxDetailDto?>(true, HttpStatusCode.OK, InboxDetail(targetId, "Loaded detail")))
        });
        this.AddModerationStub();

        var cut = RenderWithAuth<Inbox>();

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='admin-inbox-inspector']").TextContent.Should().Contain("Loaded detail"));
        cut.Find("[data-testid='admin-inbox-apply']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='admin-inbox-error']").TextContent.Should().Contain("Could not load admin inbox.");
            var inspector = cut.Find("[data-testid='admin-inbox-inspector']").TextContent;
            inspector.Should().Contain("Select a row");
            inspector.Should().NotContain("Loaded detail");
        });
    }

    [Fact]
    public void LoadsRowsAndCallsRowModerationAndReview()
    {
        int? hiddenPostId = null;
        (string TargetType, string TargetId, AdminInboxReviewRequestDto Request)? reviewed = null;

        this.AddAdminStub(new StubAdminService
        {
            GetInboxHandler = query => Task.FromResult(new ApiResult<PagedResult<AdminInboxItemDto>?>(true, HttpStatusCode.OK, new PagedResult<AdminInboxItemDto>
            {
                Items = new[]
                {
                    new AdminInboxItemDto
                    {
                        TargetType = AdminInboxItemTypes.Post,
                        TargetId = "10",
                        CreatedUtc = DateTime.UtcNow,
                        UpdatedUtc = DateTime.UtcNow,
                        AuthorUserId = "author-1",
                        AuthorName = "Painter",
                        Summary = "Spam post",
                        TargetUrl = "/posts/10",
                        State = AdminInboxStates.Reported,
                        OpenReportCount = 1
                    }
                },
                PageNumber = query.Page,
                PageSize = query.PageSize,
                TotalCount = 1
            })),
            GetInboxDetailHandler = (targetType, targetId) => Task.FromResult(new ApiResult<AdminInboxDetailDto?>(true, HttpStatusCode.OK, new AdminInboxDetailDto
            {
                TargetType = targetType,
                TargetId = targetId,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                AuthorUserId = "author-1",
                AuthorName = "Painter",
                Title = "Spam post",
                Body = "Suspicious content",
                TargetUrl = "/posts/10",
                AuditUrl = "/admin/audit?targetType=Post",
                State = AdminInboxStates.Reported,
                Reports = Array.Empty<AdminInboxReportDto>()
            })),
            ReviewInboxItemHandler = (targetType, targetId, request) =>
            {
                reviewed = (targetType, targetId, request);
                return Task.FromResult(true);
            }
        });
        this.AddModerationStub(new StubModerationService
        {
            HidePostHandler = (postId, _) =>
            {
                hiddenPostId = postId;
                return Task.FromResult(true);
            }
        });

        var cut = RenderWithAuth<Inbox>();

        cut.WaitForAssertion(() => cut.FindAll("[data-testid='admin-inbox-row']").Should().HaveCount(1));
        cut.Find("[data-testid='admin-inbox-hide']").Click();
        cut.WaitForAssertion(() => hiddenPostId.Should().Be(10));

        cut.Find("[data-testid='admin-inbox-reason']").Change("reviewed");
        cut.Find("[data-testid='admin-inbox-review']").Click();
        cut.WaitForAssertion(() =>
        {
            reviewed.Should().NotBeNull();
            reviewed!.Value.TargetType.Should().Be(AdminInboxItemTypes.Post);
            reviewed.Value.TargetId.Should().Be("10");
            reviewed.Value.Request.Reason.Should().Be("reviewed");
        });
    }

    private static AdminInboxItemDto InboxItem(string targetId, string summary) => new()
    {
        TargetType = AdminInboxItemTypes.Post,
        TargetId = targetId,
        CreatedUtc = DateTime.UtcNow,
        UpdatedUtc = DateTime.UtcNow,
        AuthorUserId = $"author-{targetId}",
        AuthorName = $"Painter {targetId}",
        Summary = summary,
        TargetUrl = $"/posts/{targetId}",
        State = AdminInboxStates.Reported,
        OpenReportCount = 1
    };

    private static AdminInboxDetailDto InboxDetail(string targetId, string body) => new()
    {
        TargetType = AdminInboxItemTypes.Post,
        TargetId = targetId,
        CreatedUtc = DateTime.UtcNow,
        UpdatedUtc = DateTime.UtcNow,
        AuthorUserId = $"author-{targetId}",
        AuthorName = $"Painter {targetId}",
        Title = $"Post {targetId}",
        Body = body,
        TargetUrl = $"/posts/{targetId}",
        AuditUrl = "/admin/audit?targetType=Post",
        State = AdminInboxStates.Reported,
        Reports = Array.Empty<AdminInboxReportDto>()
    };

    private IRenderedFragment RenderWithAuth<TComponent>() where TComponent : IComponent
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("admin");
        auth.SetRoles("Admin");

        return Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<TComponent>(0);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }
}
