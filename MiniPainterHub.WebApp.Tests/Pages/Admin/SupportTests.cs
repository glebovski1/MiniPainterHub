using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;
using AdminSupportPage = MiniPainterHub.WebApp.Pages.Admin.Support;

namespace MiniPainterHub.WebApp.Tests.Pages.Admin;

public class SupportTests : TestContext
{
    [Fact]
    public void Queue_LoadsInspectorAndAppliesSearchFilters()
    {
        this.SetAuthenticatedUser("admin-user", "admin");
        var capturedQueries = new List<(string? Search, string? Status)>();
        var ticket = StubSupportTicketService.Ticket(5, "Unsafe comment", SupportTicketCategories.Safety, "Please review this comment.");
        ticket.Status = SupportTicketStatuses.WaitingForAdmin;
        this.AddSupportStub(new StubSupportTicketService
        {
            GetAdminQueueHandler = query =>
            {
                capturedQueries.Add((query.Search, query.Status));
                return Task.FromResult(new ApiResult<PagedResult<SupportTicketSummaryDto>?>(true, HttpStatusCode.OK, new PagedResult<SupportTicketSummaryDto>
                {
                    Items = new[] { ticket },
                    PageNumber = query.PageNumber,
                    PageSize = query.PageSize,
                    TotalCount = 1
                }));
            },
            GetAdminTicketHandler = _ => Task.FromResult(new ApiResult<SupportTicketDto?>(true, HttpStatusCode.OK, ticket))
        });

        var cut = RenderComponent<AdminSupportPage>();
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='admin-support-row']").Should().HaveCount(1);
            cut.Find("[data-testid='admin-support-inspector']").TextContent.Should().Contain("Please review this comment.");
        });

        cut.Find("[data-testid='admin-support-search']").Change("unsafe");
        cut.Find("[data-testid='admin-support-status']").Change(SupportTicketStatuses.WaitingForAdmin);
        cut.Find("[data-testid='admin-support-apply']").Click();

        cut.WaitForAssertion(() => capturedQueries.Should().Contain(("unsafe", SupportTicketStatuses.WaitingForAdmin)));
    }

    [Fact]
    public void Queue_AdminCanReplyAndResolveRequest()
    {
        this.SetAuthenticatedUser("admin-user", "admin");
        CreateSupportTicketMessageDto? capturedReply = null;
        UpdateSupportTicketStatusDto? capturedStatus = null;
        var ticket = StubSupportTicketService.Ticket(5, "Upload problem", SupportTicketCategories.Bug, "The image is stuck.");
        this.AddSupportStub(new StubSupportTicketService
        {
            GetAdminQueueHandler = query => Task.FromResult(new ApiResult<PagedResult<SupportTicketSummaryDto>?>(true, HttpStatusCode.OK, new PagedResult<SupportTicketSummaryDto>
            {
                Items = new[] { ticket },
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                TotalCount = 1
            })),
            GetAdminTicketHandler = _ => Task.FromResult(new ApiResult<SupportTicketDto?>(true, HttpStatusCode.OK, ticket)),
            ReplyAsAdminHandler = (_, request) =>
            {
                capturedReply = request;
                var replied = StubSupportTicketService.Ticket(5, ticket.Subject, ticket.Category, request.Body);
                replied.Status = SupportTicketStatuses.WaitingForUser;
                return Task.FromResult(new ApiResult<SupportTicketDto?>(true, HttpStatusCode.OK, replied));
            },
            UpdateStatusHandler = (_, request) =>
            {
                capturedStatus = request;
                var resolved = StubSupportTicketService.Ticket(5, ticket.Subject, ticket.Category, "Resolved");
                resolved.Status = request.Status;
                return Task.FromResult(new ApiResult<SupportTicketDto?>(true, HttpStatusCode.OK, resolved));
            }
        });

        var cut = RenderComponent<AdminSupportPage>();
        cut.WaitForAssertion(() => cut.Find("[data-testid='admin-support-inspector']").TextContent.Should().Contain("The image is stuck."));

        cut.Find("[data-testid='admin-support-reply-input']").Change("Please retry with a smaller image.");
        cut.Find("[data-testid='admin-support-reply-submit']").Click();
        cut.WaitForAssertion(() => capturedReply!.Body.Should().Be("Please retry with a smaller image."));

        cut.Find("[data-testid='admin-support-resolve']").Click();
        cut.WaitForAssertion(() =>
        {
            capturedStatus!.Status.Should().Be(SupportTicketStatuses.Resolved);
            cut.Find("[data-testid='admin-support-inspector']").TextContent.Should().Contain("Resolved");
        });
    }

    [Fact]
    public void Queue_WhenLoadFails_ClearsInspectorAndShowsRetryState()
    {
        this.SetAuthenticatedUser("admin-user", "admin");
        this.AddSupportStub(new StubSupportTicketService
        {
            GetAdminQueueHandler = _ => Task.FromResult(new ApiResult<PagedResult<SupportTicketSummaryDto>?>(false, HttpStatusCode.InternalServerError, null))
        });

        var cut = RenderComponent<AdminSupportPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='admin-support-error']").TextContent.Should().Contain("support queue");
            cut.Find("[data-testid='admin-support-inspector']").TextContent.Should().Contain("Select a request");
        });
    }

    [Fact]
    public void Queue_SwitchingTicketsClearsReplyDraftBeforeRenderingNextRequester()
    {
        this.SetAuthenticatedUser("admin-user", "admin");
        var first = StubSupportTicketService.Ticket(5, "First request", SupportTicketCategories.Account, "First user's message");
        var second = StubSupportTicketService.Ticket(6, "Second request", SupportTicketCategories.Safety, "Second user's message");
        var replyCalls = 0;
        this.AddSupportStub(new StubSupportTicketService
        {
            GetAdminQueueHandler = query => Task.FromResult(new ApiResult<PagedResult<SupportTicketSummaryDto>?>(true, HttpStatusCode.OK, new PagedResult<SupportTicketSummaryDto>
            {
                Items = new[] { first, second },
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                TotalCount = 2
            })),
            GetAdminTicketHandler = id => Task.FromResult(new ApiResult<SupportTicketDto?>(true, HttpStatusCode.OK, id == first.Id ? first : second)),
            ReplyAsAdminHandler = (_, _) =>
            {
                replyCalls++;
                return Task.FromResult(new ApiResult<SupportTicketDto?>(true, HttpStatusCode.OK, second));
            }
        });

        var cut = RenderComponent<AdminSupportPage>();
        cut.WaitForAssertion(() => cut.Find("[data-testid='admin-support-inspector']").TextContent.Should().Contain("First user's message"));

        cut.Find("[data-testid='admin-support-reply-input']").Change("Private draft intended only for the first requester.");
        cut.FindAll("[data-testid='admin-support-select']")[1].Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='admin-support-inspector']").TextContent.Should().Contain("Second user's message");
            cut.Find("[data-testid='admin-support-reply-input']").GetAttribute("value").Should().BeNullOrEmpty();
        });

        cut.Find("[data-testid='admin-support-reply-submit']").Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("The Body field is required"));
        replyCalls.Should().Be(0);
    }
}
