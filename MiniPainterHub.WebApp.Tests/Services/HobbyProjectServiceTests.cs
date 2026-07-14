using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Services;

public sealed class HobbyProjectServiceTests
{
    [Fact]
    public async Task DiscoveryQueries_EncodeFiltersOwnerAndPagination()
    {
        var handler = new RecordingHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, EmptyProjectPageJson);
        handler.EnqueueJson(HttpStatusCode.OK, EmptyProjectPageJson);
        var service = new HobbyProjectService(CreateApiClient(handler));

        await service.GetAllAsync(new HobbyProjectQueryDto
        {
            Search = "winter force",
            Kind = HobbyProjectKinds.Army,
            Status = HobbyProjectStatuses.InProgress,
            Sort = HobbyProjectSorts.Oldest,
            PageNumber = 3,
            PageSize = 7
        });
        await service.GetByOwnerAsync("owner/with spaces", new HobbyProjectQueryDto
        {
            PageNumber = 2,
            PageSize = 3
        });

        var discovery = handler.Requests[0].Uri ?? throw new InvalidOperationException("Discovery URI was not captured.");
        discovery.AbsolutePath.Should().Be("/api/projects");
        var query = HttpUtility.ParseQueryString(discovery.Query);
        query["search"].Should().Be("winter force");
        query["kind"].Should().Be(HobbyProjectKinds.Army);
        query["status"].Should().Be(HobbyProjectStatuses.InProgress);
        query["sort"].Should().Be(HobbyProjectSorts.Oldest);
        query["pageNumber"].Should().Be("3");
        query["pageSize"].Should().Be("7");

        var ownerQuery = HttpUtility.ParseQueryString(handler.Requests[1].Uri!.Query);
        ownerQuery["ownerUserId"].Should().Be("owner/with spaces");
        ownerQuery["pageNumber"].Should().Be("2");
        ownerQuery["pageSize"].Should().Be("3");
    }

    [Fact]
    public async Task AvailablePostsQuery_EncodesSearchAndPaging()
    {
        var handler = new RecordingHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"items":[],"totalCount":0,"pageNumber":4,"pageSize":11}""");
        var service = new HobbyProjectService(CreateApiClient(handler));

        await service.GetAvailablePostsAsync(42, "  snow & rust  ", 4, 11);

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.Uri!.AbsolutePath.Should().Be("/api/projects/42/available-posts");
        var query = HttpUtility.ParseQueryString(request.Uri.Query);
        query["search"].Should().Be("snow & rust");
        query["page"].Should().Be("4");
        query["pageSize"].Should().Be("11");
    }

    [Fact]
    public async Task ReadAndCreateOperations_UseTheCompleteProjectApiSurface()
    {
        var handler = new RecordingHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, EmptyProjectPageJson);
        handler.EnqueueJson(HttpStatusCode.OK, """{"id":17,"title":"Project"}""");
        handler.EnqueueJson(HttpStatusCode.OK, """{"items":[],"totalCount":0,"pageNumber":2,"pageSize":5}""");
        handler.EnqueueJson(HttpStatusCode.OK, """{"items":[],"totalCount":0,"pageNumber":1,"pageSize":24}""");
        handler.EnqueueJson(HttpStatusCode.Created, """{"id":18,"title":"New project"}""");
        var service = new HobbyProjectService(CreateApiClient(handler));

        await service.GetMineAsync(new HobbyProjectQueryDto { IncludeArchived = true, PageNumber = 2, PageSize = 6 });
        await service.GetAsync(17);
        await service.GetDiaryAsync(17, 2, 5);
        await service.GetShowcaseAsync(17, 1, 24);
        await service.CreateAsync(new CreateHobbyProjectDto
        {
            Title = "New project",
            Description = "A complete project request.",
            Kind = HobbyProjectKinds.Miniature
        });

        handler.Requests.Select(request => (request.Method.Method, request.Uri!.AbsolutePath)).Should().Equal(
            ("GET", "/api/projects/mine"),
            ("GET", "/api/projects/17"),
            ("GET", "/api/projects/17/diary"),
            ("GET", "/api/projects/17/showcase"),
            ("POST", "/api/projects"));
        HttpUtility.ParseQueryString(handler.Requests[0].Uri!.Query)["includeArchived"].Should().Be("true");
        HttpUtility.ParseQueryString(handler.Requests[2].Uri!.Query)["page"].Should().Be("2");
        handler.Requests[4].Body.Should().Contain("New project");
        handler.Requests[4].ContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task LifecycleMutations_UseOwnerApiRoutesAndJsonBodies()
    {
        var handler = new RecordingHttpMessageHandler();
        for (var index = 0; index < 9; index++)
        {
            handler.EnqueueJson(HttpStatusCode.OK, """{"id":17,"title":"Project"}""");
        }

        var service = new HobbyProjectService(CreateApiClient(handler));
        await service.UpdateAsync(17, new UpdateHobbyProjectDto { Title = "Project", Description = "Description", Kind = HobbyProjectKinds.Army });
        await service.UpdateStatusAsync(17, new UpdateHobbyProjectStatusDto { Status = HobbyProjectStatuses.OnHold });
        await service.ArchiveAsync(17);
        await service.UnarchiveAsync(17);
        await service.LinkPostAsync(17, new LinkHobbyProjectPostDto { PostId = 31 });
        await service.UpdateEntryAsync(17, 31, new UpdateHobbyProjectEntryDto { MilestoneLabel = "Checkpoint" });
        await service.UnlinkPostAsync(17, 31);
        await service.UpdateShowcaseAsync(17, new UpdateHobbyProjectShowcaseDto { PostIds = new() { 31 } });
        await service.UpdateCoverAsync(17, new UpdateHobbyProjectCoverDto { PostId = 31 });

        handler.Requests.Select(request => (request.Method.Method, request.Uri!.AbsolutePath)).Should().Equal(
            ("PUT", "/api/projects/17"),
            ("PUT", "/api/projects/17/status"),
            ("POST", "/api/projects/17/archive"),
            ("POST", "/api/projects/17/restore"),
            ("POST", "/api/projects/17/posts"),
            ("PUT", "/api/projects/17/posts/31"),
            ("DELETE", "/api/projects/17/posts/31"),
            ("PUT", "/api/projects/17/showcase"),
            ("PUT", "/api/projects/17/cover"));
        handler.Requests.Where(request => request.Method != HttpMethod.Post || request.Uri!.AbsolutePath.EndsWith("/posts", StringComparison.Ordinal))
            .Where(request => request.Method != HttpMethod.Delete)
            .Should().Contain(request => request.Body != null && request.ContentType == "application/json");
        handler.Requests[4].Body.Should().Contain("\"postId\":31");
        handler.Requests[5].Body.Should().Contain("Checkpoint");
        handler.Requests[7].Body.Should().Contain("\"postIds\":[31]");
        handler.Requests[8].Body.Should().Contain("\"postId\":31");
    }

    private const string EmptyProjectPageJson =
        """{"items":[],"totalCount":0,"pageNumber":1,"pageSize":12}""";

    private static ApiClient CreateApiClient(RecordingHttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") },
            new NotificationRecorder());
}
