using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Notifications;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Services;

public class ModerationServiceTests
{
    [Fact]
    public async Task GetAuditAsync_WhenFiltersAreProvided_IncludesEachFilterInQueryString()
    {
        var handler = new CapturingHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var apiClient = new ApiClient(httpClient, new NoOpNotificationService());
        var service = new ModerationService(apiClient);

        var result = await service.GetAuditAsync(new ModerationAuditQueryDto
        {
            Page = 2,
            PageSize = 25,
            TargetType = "Post",
            ActorUserId = "user/1",
            ActionType = "Post Hide"
        });

        result.Success.Should().BeTrue();
        var query = handler.LastRequestUri?.Query;
        query.Should().NotBeNull();
        query.Should().Contain("page=2");
        query.Should().Contain("pageSize=25");
        query.Should().Contain("targetType=Post");
        query.Should().Contain("actorUserId=user%2F1");
        query.Should().Contain("actionType=Post%20Hide");
    }

    [Fact]
    public async Task GetAuditAsync_WhenFiltersAreEmpty_OmitsOptionalFilterParameters()
    {
        var handler = new CapturingHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var apiClient = new ApiClient(httpClient, new NoOpNotificationService());
        var service = new ModerationService(apiClient);

        var result = await service.GetAuditAsync(new ModerationAuditQueryDto
        {
            Page = 1,
            PageSize = 20,
            TargetType = " ",
            ActorUserId = null,
            ActionType = string.Empty
        });

        result.Success.Should().BeTrue();
        var query = handler.LastRequestUri?.Query;
        query.Should().NotBeNull();
        query.Should().Contain("page=1");
        query.Should().Contain("pageSize=20");
        query.Should().NotContain("targetType=");
        query.Should().NotContain("actorUserId=");
        query.Should().NotContain("actionType=");
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private static readonly string EmptyPagedResultJson = "{\"items\":[],\"totalCount\":0,\"pageNumber\":1,\"pageSize\":20}";

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(EmptyPagedResultJson, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }

    private sealed class NoOpNotificationService : INotificationService
    {
        public ValueTask ShowSuccessAsync(string message, string? header = null) => ValueTask.CompletedTask;
        public ValueTask ShowInfoAsync(string message, string? header = null) => ValueTask.CompletedTask;
        public ValueTask ShowWarningAsync(string message, string? header = null) => ValueTask.CompletedTask;
        public ValueTask ShowErrorAsync(string message, string? header = null) => ValueTask.CompletedTask;
        public ValueTask ShowValidationErrorsAsync(IDictionary<string, string[]> errors) => ValueTask.CompletedTask;
    }
}
