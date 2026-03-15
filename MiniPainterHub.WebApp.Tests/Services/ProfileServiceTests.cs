using System;
using System.Collections.Generic;
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

public class ProfileServiceTests
{
    [Fact]
    public async Task GetMineAsync_WhenProfileIsCached_AvoidsSecondRequest()
    {
        var handler = new RecordingHttpMessageHandler();
        var service = new ProfileService(CreateApiClient(handler));
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """{"userId":"user-1","displayName":"Painter","bio":"Blends"}""");

        var first = await service.GetMineAsync();
        var second = await service.GetMineAsync();

        first?.DisplayName.Should().Be("Painter");
        second?.DisplayName.Should().Be("Painter");
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task GetMineAsync_WhenProfileIsMissing_ClearsCacheAndRaisesMineChanged()
    {
        var handler = new RecordingHttpMessageHandler();
        var service = new ProfileService(CreateApiClient(handler));
        var events = new List<string?>();
        service.MineChanged += profile => events.Add(profile?.DisplayName);
        handler.EnqueueJson(HttpStatusCode.NotFound, """{"title":"Not found","status":404}""");

        var profile = await service.GetMineAsync();

        profile.Should().BeNull();
        service.Mine.Should().BeNull();
        events.Should().ContainSingle().Which.Should().BeNull();
    }

    [Fact]
    public async Task CreateMineAsync_AndUpdateMineAsync_RefreshCacheAndRaiseEvents()
    {
        var handler = new RecordingHttpMessageHandler();
        var service = new ProfileService(CreateApiClient(handler));
        var events = new List<string?>();
        service.MineChanged += profile => events.Add(profile?.DisplayName);
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """{"userId":"user-1","displayName":"Painter","bio":"Started"}""");
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """{"userId":"user-1","displayName":"Painter Prime","bio":"Updated"}""");

        var created = await service.CreateMineAsync(new MiniPainterHub.Common.DTOs.CreateUserProfileDto
        {
            DisplayName = "Painter",
            Bio = "Started"
        });
        var updated = await service.UpdateMineAsync(new MiniPainterHub.Common.DTOs.UpdateUserProfileDto
        {
            DisplayName = "Painter Prime",
            Bio = "Updated"
        });

        created.DisplayName.Should().Be("Painter");
        updated.DisplayName.Should().Be("Painter Prime");
        service.Mine?.DisplayName.Should().Be("Painter Prime");
        events.Should().BeEquivalentTo("Painter", "Painter Prime");
        handler.Requests.Select(request => request.Method.Method).Should().Equal(HttpMethod.Post.Method, HttpMethod.Put.Method);
    }

    [Fact]
    public async Task UploadAvatarAsync_AndRemoveAvatarAsync_SendExpectedRequests()
    {
        var handler = new RecordingHttpMessageHandler();
        var service = new ProfileService(CreateApiClient(handler));
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """{"userId":"user-1","displayName":"Painter","avatarUrl":"/avatars/one.webp"}""");
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """{"userId":"user-1","displayName":"Painter","avatarUrl":null}""");

        var uploaded = await service.UploadAvatarAsync(new FakeBrowserFile("avatar.png", "image/png", new byte[] { 1, 2, 3 }));
        var removed = await service.RemoveAvatarAsync();

        uploaded.AvatarUrl.Should().Be("/avatars/one.webp");
        removed.AvatarUrl.Should().BeNull();
        handler.Requests[0].Uri.Should().Be(new Uri("https://example.test/api/profiles/me/avatar"));
        handler.Requests[0].ContentType.Should().StartWith("multipart/form-data");
        handler.Requests[1].Method.Should().Be(HttpMethod.Delete);
        handler.Requests[1].Uri.Should().Be(new Uri("https://example.test/api/profiles/me/avatar"));
    }

    [Fact]
    public async Task GetPublicProfileById_WhenCalled_ReturnsRemoteProfile()
    {
        var handler = new RecordingHttpMessageHandler();
        var service = new ProfileService(CreateApiClient(handler));
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """{"userId":"user/42","displayName":"Remote Painter","bio":"Profile"}""");

        var profile = await service.GetPublicProfileById("user/42");

        profile.DisplayName.Should().Be("Remote Painter");
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Uri.Should().Be(new Uri("https://example.test/api/profiles/user/42"));
    }

    [Fact]
    public void ClearCache_WhenCalled_RaisesMineChangedWithNull()
    {
        var service = new ProfileService(CreateApiClient(new RecordingHttpMessageHandler()));
        var events = new List<string?>();
        service.MineChanged += profile => events.Add(profile?.DisplayName);

        service.ClearCache();

        events.Should().ContainSingle().Which.Should().BeNull();
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
