using System;
using System.Net;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages.Admin;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages.Admin;

public class InboxTests : TestContext
{
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
