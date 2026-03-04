using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages.Admin;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages.Admin;

public class AuditLogTests : TestContext
{
    [Fact]
    public void OnLoad_RequestsFirstPageAndRendersRows()
    {
        var calls = new List<ModerationAuditQueryDto>();
        this.AddModerationStub(new StubModerationService
        {
            GetAuditHandler = query =>
            {
                calls.Add(CloneQuery(query));
                return Task.FromResult(new ApiResult<PagedResult<ModerationAuditDto>?>(true, HttpStatusCode.OK, new PagedResult<ModerationAuditDto>
                {
                    Items = new[]
                    {
                        new ModerationAuditDto
                        {
                            Id = 1,
                            CreatedUtc = new DateTime(2026, 3, 4, 12, 0, 0, DateTimeKind.Utc),
                            ActorUserId = "admin-1",
                            ActorRole = "Admin",
                            ActionType = "PostHide",
                            TargetType = "Post",
                            TargetId = "22"
                        }
                    },
                    TotalCount = 1,
                    PageNumber = query.Page,
                    PageSize = query.PageSize
                }));
            }
        });

        var cut = RenderComponent<AuditLog>();

        cut.WaitForAssertion(() =>
        {
            calls.Should().NotBeEmpty();
            calls[0].Page.Should().Be(1);
            calls[0].PageSize.Should().Be(20);
            cut.FindAll("[data-testid='audit-row']").Should().HaveCount(1);
            cut.Find("[data-testid='audit-page-state']").TextContent.Should().Contain("Page 1");
        });
    }

    [Fact]
    public async Task ApplyFilters_SendsFiltersAndResetsToFirstPage()
    {
        var calls = new List<ModerationAuditQueryDto>();
        this.AddModerationStub(new StubModerationService
        {
            GetAuditHandler = query =>
            {
                calls.Add(CloneQuery(query));
                return Task.FromResult(new ApiResult<PagedResult<ModerationAuditDto>?>(true, HttpStatusCode.OK, new PagedResult<ModerationAuditDto>
                {
                    Items = Array.Empty<ModerationAuditDto>(),
                    TotalCount = 0,
                    PageNumber = query.Page,
                    PageSize = query.PageSize
                }));
            }
        });

        var cut = RenderComponent<AuditLog>();
        cut.WaitForAssertion(() => calls.Should().NotBeEmpty());
        calls.Clear();

        cut.Find("[data-testid='audit-filter-target-type']").Change("User");
        cut.Find("[data-testid='audit-filter-actor-user-id']").Change("admin-1");
        cut.Find("[data-testid='audit-filter-action-type']").Change("UserSuspend");
        cut.Find("[data-testid='audit-filter-page-size']").Change("50");
        await cut.Find("[data-testid='audit-apply-filters']").ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            calls.Should().HaveCount(1);
            calls[0].Page.Should().Be(1);
            calls[0].PageSize.Should().Be(50);
            calls[0].TargetType.Should().Be("User");
            calls[0].ActorUserId.Should().Be("admin-1");
            calls[0].ActionType.Should().Be("UserSuspend");
        });
    }

    [Fact]
    public async Task NextPage_LoadsSecondPage()
    {
        var calls = new List<ModerationAuditQueryDto>();
        this.AddModerationStub(new StubModerationService
        {
            GetAuditHandler = query =>
            {
                calls.Add(CloneQuery(query));
                return Task.FromResult(new ApiResult<PagedResult<ModerationAuditDto>?>(true, HttpStatusCode.OK, new PagedResult<ModerationAuditDto>
                {
                    Items = Array.Empty<ModerationAuditDto>(),
                    TotalCount = 45,
                    PageNumber = query.Page,
                    PageSize = query.PageSize
                }));
            }
        });

        var cut = RenderComponent<AuditLog>();
        cut.WaitForAssertion(() => calls.Should().NotBeEmpty());
        calls.Clear();

        await cut.Find("[data-testid='audit-next']").ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            calls.Should().HaveCount(1);
            calls.Single().Page.Should().Be(2);
        });
    }

    private static ModerationAuditQueryDto CloneQuery(ModerationAuditQueryDto query)
    {
        return new ModerationAuditQueryDto
        {
            Page = query.Page,
            PageSize = query.PageSize,
            TargetType = query.TargetType,
            ActorUserId = query.ActorUserId,
            ActionType = query.ActionType
        };
    }
}
