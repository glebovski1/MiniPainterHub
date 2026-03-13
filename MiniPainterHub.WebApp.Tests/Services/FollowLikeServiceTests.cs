using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using MiniPainterHub.WebApp.Services;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Services;

public class FollowLikeServiceTests
{
    [Fact]
    public async Task FollowService_FollowAsync_EncodesUserIdInRoute()
    {
        var handler = new RecordingHttpMessageHandler();
        var service = new FollowService(CreateApiClient(handler));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));

        await service.FollowAsync("user/with spaces");

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].Uri.Should().Be(new Uri("https://example.test/api/follows/user%2Fwith%20spaces"));
    }

    [Fact]
    public async Task FollowService_GetFollowersAndFollowingAsync_WhenApiReturnsNoContent_ReturnEmptyLists()
    {
        var handler = new RecordingHttpMessageHandler();
        var service = new FollowService(CreateApiClient(handler));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));

        var followers = await service.GetFollowersAsync();
        var following = await service.GetFollowingAsync();

        followers.Should().BeEmpty();
        following.Should().BeEmpty();
        handler.Requests.Select(request => request.Uri!.ToString()).Should().Equal(
            "https://example.test/api/follows/me/followers",
            "https://example.test/api/follows/me/following");
    }

    [Fact]
    public async Task LikeService_GetLikesAsync_WhenApiReturnsNoContent_ReturnsDefaultDto()
    {
        var handler = new RecordingHttpMessageHandler();
        var service = new LikeService(CreateApiClient(handler));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));

        var dto = await service.GetLikesAsync(14);

        dto.Count.Should().Be(0);
        dto.UserHasLiked.Should().BeFalse();
    }

    [Fact]
    public async Task LikeService_ToggleLikeAsync_PostsThenReloadsCounts()
    {
        var handler = new RecordingHttpMessageHandler();
        var service = new LikeService(CreateApiClient(handler));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        handler.EnqueueJson(HttpStatusCode.OK, """{"count":3,"userHasLiked":true}""");

        var dto = await service.ToggleLikeAsync(9);

        dto.Count.Should().Be(3);
        dto.UserHasLiked.Should().BeTrue();
        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].Uri.Should().Be(new Uri("https://example.test/api/posts/9/likes"));
        handler.Requests[1].Method.Should().Be(HttpMethod.Get);
        handler.Requests[1].Uri.Should().Be(new Uri("https://example.test/api/posts/9/likes"));
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
