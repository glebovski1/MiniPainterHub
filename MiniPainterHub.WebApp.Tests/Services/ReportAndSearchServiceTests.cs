using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Services;

public class ReportAndSearchServiceTests
{
    [Fact]
    public async Task ReportService_ReportUserAsync_EncodesUserId()
    {
        var handler = new RecordingHttpMessageHandler();
        var service = new ReportService(CreateApiClient(handler));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));

        var success = await service.ReportUserAsync("user/with spaces", new CreateReportRequestDto { ReasonCode = ReportReasonCodes.Spam });

        success.Should().BeTrue();
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Uri.Should().Be(new Uri("https://example.test/api/reports/users/user%2Fwith%20spaces"));
    }

    [Fact]
    public async Task ReportService_GetQueueAsync_AndResolveAsync_UseExpectedRoutes()
    {
        var handler = new RecordingHttpMessageHandler();
        var service = new ReportService(CreateApiClient(handler));
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """{"items":[],"totalCount":0,"pageNumber":2,"pageSize":25}""");
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));

        var queue = await service.GetQueueAsync(new ReportQueueQueryDto
        {
            Page = 2,
            PageSize = 25,
            Status = ReportStatuses.Open,
            TargetType = ReportTargetTypes.Comment,
            ReasonCode = ReportReasonCodes.Other
        });
        var resolved = await service.ResolveAsync(42, new ResolveReportRequestDto
        {
            Status = ReportStatuses.Actioned,
            ResolutionNote = "Handled"
        });

        queue.Success.Should().BeTrue();
        resolved.Should().BeTrue();
        handler.Requests[0].Uri!.Query.Should().Contain("page=2");
        handler.Requests[0].Uri!.Query.Should().Contain("pageSize=25");
        handler.Requests[0].Uri!.Query.Should().Contain("status=Open");
        handler.Requests[0].Uri!.Query.Should().Contain("targetType=Comment");
        handler.Requests[0].Uri!.Query.Should().Contain("reasonCode=Other");
        handler.Requests[1].Uri.Should().Be(new Uri("https://example.test/api/reports/42/resolve"));
    }

    [Fact]
    public async Task SearchService_MethodsBuildExpectedQueries()
    {
        var handler = new RecordingHttpMessageHandler();
        var service = new SearchService(CreateApiClient(handler));
        handler.EnqueueJson(HttpStatusCode.OK, """{"posts":[],"users":[],"tags":[]}""");
        handler.EnqueueJson(HttpStatusCode.OK, """{"items":[],"totalCount":0,"pageNumber":1,"pageSize":10}""");
        handler.EnqueueJson(HttpStatusCode.OK, """{"items":[],"totalCount":0,"pageNumber":2,"pageSize":5}""");
        handler.EnqueueJson(HttpStatusCode.OK, """{"items":[],"totalCount":0,"pageNumber":3,"pageSize":7}""");

        await service.GetOverviewAsync("glaze blend");
        await service.SearchPostsAsync("glaze blend", "nmm-highlights", 1, 10);
        await service.SearchUsersAsync("artist one", 2, 5);
        await service.SearchTagsAsync("weathering", 3, 7);

        var overviewUri = handler.Requests[0].Uri ?? throw new InvalidOperationException("Overview request URI was not captured.");
        var postsUri = handler.Requests[1].Uri ?? throw new InvalidOperationException("Posts request URI was not captured.");
        var usersUri = handler.Requests[2].Uri ?? throw new InvalidOperationException("Users request URI was not captured.");
        var tagsUri = handler.Requests[3].Uri ?? throw new InvalidOperationException("Tags request URI was not captured.");

        overviewUri.AbsolutePath.Should().Be("/api/search/overview");
        HttpUtility.ParseQueryString(overviewUri.Query)["q"].Should().Be("glaze blend");

        postsUri.AbsolutePath.Should().Be("/api/search/posts");
        var postsQuery = HttpUtility.ParseQueryString(postsUri.Query);
        postsQuery["q"].Should().Be("glaze blend");
        postsQuery["tag"].Should().Be("nmm-highlights");
        postsQuery["page"].Should().Be("1");
        postsQuery["pageSize"].Should().Be("10");

        usersUri.AbsolutePath.Should().Be("/api/search/users");
        var usersQuery = HttpUtility.ParseQueryString(usersUri.Query);
        usersQuery["q"].Should().Be("artist one");
        usersQuery["page"].Should().Be("2");
        usersQuery["pageSize"].Should().Be("5");

        tagsUri.AbsolutePath.Should().Be("/api/search/tags");
        var tagsQuery = HttpUtility.ParseQueryString(tagsUri.Query);
        tagsQuery["q"].Should().Be("weathering");
        tagsQuery["page"].Should().Be("3");
        tagsQuery["pageSize"].Should().Be("7");
    }

    private static ApiClient CreateApiClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        };

        return new ApiClient(httpClient, new NotificationRecorder());
    }
}
