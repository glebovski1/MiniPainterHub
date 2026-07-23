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
using SupportDetailsPage = MiniPainterHub.WebApp.Pages.SupportDetails;
using SupportListPage = MiniPainterHub.WebApp.Pages.Support;
using SupportCreatePage = MiniPainterHub.WebApp.Pages.SupportCreate;

namespace MiniPainterHub.WebApp.Tests.Pages;

public class SupportPagesTests : BunitContext
{
    [Fact]
    public void List_RendersUnreadTicketsAndNavigatesToSelectedRequest()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");
        this.AddSupportStub(new StubSupportTicketService
        {
            RefreshUnreadCountHandler = () => Task.FromResult(1),
            GetMineHandler = query => Task.FromResult(new ApiResult<PagedResult<SupportTicketSummaryDto>?>(true, HttpStatusCode.OK, new PagedResult<SupportTicketSummaryDto>
            {
                Items = new[]
                {
                    Summary(8, "Account locked", SupportTicketStatuses.WaitingForUser, unread: true),
                    Summary(7, "Image upload", SupportTicketStatuses.WaitingForAdmin)
                },
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                TotalCount = 2
            }))
        });

        var cut = Render<SupportListPage>();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='support-ticket-card']").Should().HaveCount(2);
            cut.Markup.Should().Contain("Account locked");
            cut.Markup.Should().Contain("New reply");
        });

        cut.FindAll("[data-testid='support-ticket-card']")[0].Click();
        this.CurrentPath().Should().Be("/support/8");
    }

    [Fact]
    public void Create_WhenRequestFails_PreservesDraftForRetry()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");
        var submittedDrafts = new List<(string Category, string Subject, string Message)>();
        this.AddSupportStub(new StubSupportTicketService
        {
            CreateHandler = request =>
            {
                submittedDrafts.Add((request.Category, request.Subject, request.Message));
                return Task.FromResult(new ApiResult<SupportTicketDto?>(false, HttpStatusCode.InternalServerError, null));
            }
        });

        var cut = Render<SupportCreatePage>();
        cut.Find("[data-testid='support-category']").Change(SupportTicketCategories.Bug);
        cut.Find("[data-testid='support-subject']").Change("Upload keeps failing");
        cut.Find("[data-testid='support-message-input']").Change("I tried twice and both uploads stopped.");
        cut.Find("[data-testid='support-create-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='support-create-error']").TextContent.Should().Contain("draft is still here");
            cut.Find("[data-testid='support-subject']").GetAttribute("value").Should().Be("Upload keeps failing");
            submittedDrafts.Should().ContainSingle().Which.Should().Be((SupportTicketCategories.Bug, "Upload keeps failing", "I tried twice and both uploads stopped."));
        });

        cut.Find("[data-testid='support-create-submit']").Click();
        cut.WaitForAssertion(() => submittedDrafts.Should().HaveCount(2));
        submittedDrafts[1].Should().Be(submittedDrafts[0]);
    }

    [Fact]
    public void Create_WhenRequestSucceeds_NavigatesToTicket()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");
        this.AddSupportStub(new StubSupportTicketService
        {
            CreateHandler = request => Task.FromResult(new ApiResult<SupportTicketDto?>(true, HttpStatusCode.Created,
                StubSupportTicketService.Ticket(42, request.Subject, request.Category, request.Message)))
        });

        var cut = Render<SupportCreatePage>();
        cut.Find("[data-testid='support-category']").Change(SupportTicketCategories.Suggestion);
        cut.Find("[data-testid='support-subject']").Change("Add paint inventory");
        cut.Find("[data-testid='support-message-input']").Change("It would help track paints.");
        cut.Find("[data-testid='support-create-submit']").Click();

        cut.WaitForAssertion(() => this.CurrentPath().Should().Be("/support/42"));
    }

    [Fact]
    public void Details_MarksUnreadReplyReadAndReplyReopensResolvedTicket()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");
        var markedRead = new List<int>();
        DateTime? observedStaffReplyUtc = null;
        var resolved = StubSupportTicketService.Ticket(12, "Account email", SupportTicketCategories.Account, "Original question");
        resolved.Status = SupportTicketStatuses.Resolved;
        resolved.HasUnreadStaffReply = true;
        resolved.LastStaffReplyUtc = DateTime.UtcNow.AddMinutes(-2);
        resolved.Messages = new[]
        {
            resolved.Messages[0],
            new SupportTicketMessageDto
            {
                Id = 2,
                TicketId = 12,
                AuthorUserId = "admin-user",
                AuthorUserName = "admin",
                AuthorDisplayName = "Admin Painter",
                Body = "We updated the email.",
                SentUtc = DateTime.UtcNow,
                IsStaffReply = true
            }
        };

        this.AddSupportStub(new StubSupportTicketService
        {
            GetHandler = _ => Task.FromResult(new ApiResult<SupportTicketDto?>(true, HttpStatusCode.OK, resolved)),
            MarkReadHandler = (id, lastStaffReplyUtc) =>
            {
                markedRead.Add(id);
                observedStaffReplyUtc = lastStaffReplyUtc;
                return Task.FromResult(true);
            },
            ReplyHandler = (_, request) =>
            {
                var reopened = StubSupportTicketService.Ticket(12, "Account email", SupportTicketCategories.Account, request.Body);
                reopened.Status = SupportTicketStatuses.WaitingForAdmin;
                return Task.FromResult(new ApiResult<SupportTicketDto?>(true, HttpStatusCode.OK, reopened));
            }
        });

        var cut = Render<SupportDetailsPage>(parameters => parameters.Add(page => page.Id, 12));
        cut.WaitForAssertion(() =>
        {
            markedRead.Should().Contain(12);
            observedStaffReplyUtc.Should().Be(resolved.LastStaffReplyUtc);
            cut.Find("[data-testid='support-resolved-state']").Should().NotBeNull();
            cut.Markup.Should().Contain("We updated the email.");
        });

        cut.Find("[data-testid='support-reply-input']").Change("I still need help.");
        cut.Find("[data-testid='support-reply-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Waiting for admin");
            cut.Markup.Should().Contain("I still need help.");
            cut.FindAll("[data-testid='support-resolved-state']").Should().BeEmpty();
        });
    }

    [Fact]
    public void List_WhenLoadingFails_RendersRetryState()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");
        this.AddSupportStub(new StubSupportTicketService
        {
            GetMineHandler = _ => Task.FromResult(new ApiResult<PagedResult<SupportTicketSummaryDto>?>(false, HttpStatusCode.InternalServerError, null))
        });

        var cut = Render<SupportListPage>();

        cut.WaitForAssertion(() => cut.Find("[data-testid='support-error']").TextContent.Should().Contain("support history"));
    }

    private static SupportTicketSummaryDto Summary(int id, string subject, string status, bool unread = false) => new()
    {
        Id = id,
        Category = SupportTicketCategories.Account,
        Subject = subject,
        Status = status,
        CreatedUtc = DateTime.UtcNow.AddDays(-1),
        UpdatedUtc = DateTime.UtcNow,
        HasUnreadStaffReply = unread,
        LatestMessagePreview = "Latest request update",
        RequesterUserId = "viewer-user",
        RequesterUserName = "viewer",
        RequesterDisplayName = "Viewer Painter"
    };
}
