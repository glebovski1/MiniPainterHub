using System;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Services;

public class SupportTicketServiceTests
{
    [Fact]
    public async Task UserAndAdminQueries_UseExpectedRoutesAndEscapedFilters()
    {
        var (service, handler) = CreateService();
        const string pageJson = "{\"items\":[],\"totalCount\":0,\"pageNumber\":2,\"pageSize\":10}";
        handler.EnqueueJson(HttpStatusCode.OK, pageJson);
        handler.EnqueueJson(HttpStatusCode.OK, pageJson);

        var query = new SupportTicketQueryDto
        {
            PageNumber = 2,
            PageSize = 10,
            Status = SupportTicketStatuses.WaitingForAdmin,
            Category = SupportTicketCategories.Safety,
            Search = "account / login"
        };

        await service.GetMineAsync(query);
        await service.GetAdminQueueAsync(query);

        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].Uri!.AbsolutePath.Should().Be("/api/support/tickets");
        handler.Requests[1].Uri!.AbsolutePath.Should().Be("/api/admin/support/tickets");
        foreach (var request in handler.Requests)
        {
            request.Uri!.Query.Should().Contain("pageNumber=2");
            request.Uri.Query.Should().Contain("pageSize=10");
            request.Uri.Query.Should().Contain("status=WaitingForAdmin");
            request.Uri.Query.Should().Contain("category=Safety");
            request.Uri.Query.Should().Contain("search=account%20%2F%20login");
        }
    }

    [Fact]
    public async Task Mutations_UseExpectedMethodsRoutesAndJsonBodies()
    {
        var (service, handler) = CreateService();
        const string ticketJson = "{\"id\":7,\"category\":\"Bug\",\"subject\":\"Upload issue\",\"status\":\"New\",\"createdUtc\":\"2026-07-12T12:00:00Z\",\"updatedUtc\":\"2026-07-12T12:00:00Z\",\"latestMessagePreview\":\"Details\",\"requesterUserId\":\"u1\",\"requesterUserName\":\"user\",\"requesterDisplayName\":\"User\",\"messages\":[]}";
        handler.EnqueueJson(HttpStatusCode.Created, ticketJson);
        handler.EnqueueJson(HttpStatusCode.OK, ticketJson);
        handler.EnqueueJson(HttpStatusCode.OK, ticketJson);
        handler.EnqueueJson(HttpStatusCode.OK, ticketJson);

        await service.CreateAsync(new CreateSupportTicketDto { Category = SupportTicketCategories.Bug, Subject = "Upload issue", Message = "Details" });
        await service.ReplyAsync(7, new CreateSupportTicketMessageDto { Body = "More detail" });
        await service.ReplyAsAdminAsync(7, new CreateSupportTicketMessageDto { Body = "Admin answer" });
        await service.UpdateStatusAsync(7, new UpdateSupportTicketStatusDto { Status = SupportTicketStatuses.Resolved });

        handler.Requests.Select(request => (request.Method, request.Uri!.AbsolutePath)).Should().Equal(
            (HttpMethod.Post, "/api/support/tickets"),
            (HttpMethod.Post, "/api/support/tickets/7/messages"),
            (HttpMethod.Post, "/api/admin/support/tickets/7/messages"),
            (HttpMethod.Put, "/api/admin/support/tickets/7/status"));
        handler.Requests[0].Body.Should().Contain("\"subject\":\"Upload issue\"");
        handler.Requests[1].Body.Should().Contain("\"body\":\"More detail\"");
        handler.Requests[2].Body.Should().Contain("\"body\":\"Admin answer\"");
        handler.Requests[3].Body.Should().Contain("\"status\":\"Resolved\"");
    }

    [Fact]
    public async Task MarkRead_RefreshesUnreadCountAndRaisesChange()
    {
        var (service, handler) = CreateService();
        handler.EnqueueJson(HttpStatusCode.NoContent);
        handler.EnqueueJson(HttpStatusCode.OK, "{\"count\":3}");
        var changes = 0;
        service.UnreadCountChanged += () => changes++;

        var observedReplyUtc = new DateTime(2026, 7, 12, 14, 30, 0, DateTimeKind.Utc);
        var success = await service.MarkReadAsync(9, observedReplyUtc);

        success.Should().BeTrue();
        service.UnreadCount.Should().Be(3);
        changes.Should().Be(1);
        handler.Requests.Select(request => request.Uri!.AbsolutePath).Should().Equal(
            "/api/support/tickets/9/read",
            "/api/support/tickets/unread-count");
        handler.Requests[0].Body.Should().Contain("\"lastStaffReplyUtc\":\"2026-07-12T14:30:00Z\"");
    }

    private static (SupportTicketService Service, RecordingHttpMessageHandler Handler) CreateService()
    {
        var handler = new RecordingHttpMessageHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var api = new ApiClient(client, new NotificationRecorder());
        return (new SupportTicketService(api), handler);
    }
}
