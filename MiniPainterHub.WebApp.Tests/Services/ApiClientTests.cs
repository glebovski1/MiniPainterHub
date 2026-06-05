using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Services;

public class ApiClientTests
{
    [Fact]
    public async Task SendForResultAsync_WhenBadRequestContainsValidationErrors_UsesValidationNotifications()
    {
        var handler = new RecordingHttpMessageHandler();
        var notifications = new NotificationRecorder();
        var client = CreateClient(handler, notifications);
        handler.EnqueueJson(
            HttpStatusCode.BadRequest,
            """
            {
              "title": "Validation failed",
              "status": 400,
              "errors": {
                "title": ["Title is required."],
                "content": "Content is required."
              }
            }
            """);

        var result = await client.SendForResultAsync<object>(new HttpRequestMessage(HttpMethod.Post, "api/posts"));

        result.Success.Should().BeFalse();
        notifications.ValidationErrors.Should().ContainSingle();
        notifications.ValidationErrors[0]["title"].Should().Contain("Title is required.");
        notifications.ValidationErrors[0]["content"].Should().Contain("Content is required.");
        notifications.ErrorCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenUnauthorized_ShowsAuthenticationPrompt()
    {
        var handler = new RecordingHttpMessageHandler();
        var notifications = new NotificationRecorder();
        var client = CreateClient(handler, notifications);
        handler.EnqueueJson(HttpStatusCode.Unauthorized, """{ "title": "Unauthorized", "status": 401 }""");

        var success = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "api/profiles/me"));

        success.Should().BeFalse();
        notifications.InfoCalls.Should().ContainSingle();
        notifications.InfoCalls[0].Header.Should().Be("Authentication required");
        notifications.InfoCalls[0].Message.Should().Contain("Please sign in");
    }

    [Fact]
    public async Task SendForResultAsync_WhenStatusIsSuppressed_DoesNotRaiseErrorNotification()
    {
        var handler = new RecordingHttpMessageHandler();
        var notifications = new NotificationRecorder();
        var client = CreateClient(handler, notifications);
        handler.EnqueueJson(HttpStatusCode.ServiceUnavailable, """{ "title": "Maintenance", "status": 503 }""");

        var result = await client.SendForResultAsync<object>(
            new HttpRequestMessage(HttpMethod.Get, "api/healthz"),
            new ApiRequestOptions
            {
                SuppressedStatusCodes = new HashSet<HttpStatusCode> { HttpStatusCode.ServiceUnavailable }
            });

        result.Success.Should().BeFalse();
        notifications.ErrorCalls.Should().BeEmpty();
        notifications.InfoCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task SendForResultAsync_WhenResponseIsNoContent_ReturnsSuccessfulDefault()
    {
        var handler = new RecordingHttpMessageHandler();
        var notifications = new NotificationRecorder();
        var client = CreateClient(handler, notifications);
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));

        var result = await client.SendForResultAsync<object>(new HttpRequestMessage(HttpMethod.Delete, "api/posts/42"));

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.NoContent);
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task SendForResultAsync_WhenResponseIsOkWithEmptyBody_ReturnsSuccessfulDefault()
    {
        var handler = new RecordingHttpMessageHandler();
        var notifications = new NotificationRecorder();
        var client = CreateClient(handler, notifications);
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty)
        });

        var result = await client.SendForResultAsync<object>(new HttpRequestMessage(HttpMethod.Get, "api/search/overview"));

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Value.Should().BeNull();
        notifications.ErrorCalls.Should().BeEmpty();
        notifications.ValidationErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task SendForResultAsync_WhenRequestCompletes_RecordsApiDurationMetric()
    {
        var handler = new RecordingHttpMessageHandler();
        var notifications = new NotificationRecorder();
        var metrics = new RecordingClientPerformanceMetrics();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var client = new ApiClient(httpClient, notifications, metrics);
        handler.EnqueueJson(HttpStatusCode.OK, """{ "value": "ok" }""");

        await client.SendForResultAsync<object>(new HttpRequestMessage(HttpMethod.Get, "api/posts?page=1"));

        metrics.ApiRequests.Should().ContainSingle();
        metrics.ApiRequests.Single().RequestUri!.AbsolutePath.Should().Be("/api/posts");
        metrics.ApiRequests.Single().StatusCode.Should().Be(200);
        metrics.ApiRequests.Single().Success.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_WhenAuthTokenExists_AddsBearerTokenWithoutHttpClientFactory()
    {
        var handler = new RecordingHttpMessageHandler();
        var notifications = new NotificationRecorder();
        var tokenStore = new RecordingTokenStore { Token = "stored-token" };
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var client = new ApiClient(httpClient, notifications, tokenStore: tokenStore);
        handler.EnqueueJson(HttpStatusCode.OK, """{ "ok": true }""");

        var success = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "api/posts"));

        success.Should().BeTrue();
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Authorization.Should().NotBeNull();
        handler.Requests[0].Authorization!.Scheme.Should().Be("Bearer");
        handler.Requests[0].Authorization!.Parameter.Should().Be("stored-token");
        tokenStore.GetCalls.Should().Be(1);
    }

    [Fact]
    public async Task SendAsync_WhenRequestAlreadyHasAuthorizationHeader_DoesNotReadOrReplaceToken()
    {
        var handler = new RecordingHttpMessageHandler();
        var notifications = new NotificationRecorder();
        var tokenStore = new RecordingTokenStore { Token = "stored-token" };
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var client = new ApiClient(httpClient, notifications, tokenStore: tokenStore);
        handler.EnqueueJson(HttpStatusCode.OK, """{ "ok": true }""");
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/posts");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "explicit-token");

        var success = await client.SendAsync(request);

        success.Should().BeTrue();
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Authorization.Should().NotBeNull();
        handler.Requests[0].Authorization!.Scheme.Should().Be("Bearer");
        handler.Requests[0].Authorization!.Parameter.Should().Be("explicit-token");
        tokenStore.GetCalls.Should().Be(0);
    }

    [Fact]
    public async Task SendForResultAsync_WhenRequestTimesOut_ShowsTimeoutNotification()
    {
        var notifications = new NotificationRecorder();
        var client = CreateClient(new TimeoutHttpMessageHandler(), notifications);

        var result = await client.SendForResultAsync<object>(new HttpRequestMessage(HttpMethod.Get, "api/posts"));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().BeNull();
        notifications.ErrorCalls.Should().ContainSingle();
        notifications.ErrorCalls[0].Header.Should().Be("Request timed out");
    }

    private static ApiClient CreateClient(HttpMessageHandler handler, NotificationRecorder notifications)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        };

        return new ApiClient(httpClient, notifications);
    }

    private sealed class TimeoutHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new OperationCanceledException();
    }

    private sealed class RecordingClientPerformanceMetrics : MiniPainterHub.WebApp.Services.Performance.IClientPerformanceMetrics
    {
        public List<ApiRequestMetric> ApiRequests { get; } = new();

        public bool IsEnabled => true;

        public void EnableForSession()
        {
        }

        public void RecordMetric(string name, double value, string unit, string? path = null)
        {
        }

        public void RecordMetric(MiniPainterHub.Common.DTOs.ClientPerformanceMetricDto metric)
        {
        }

        public void RecordApiRequest(HttpMethod method, Uri? requestUri, double durationMs, int? statusCode, bool success)
        {
            ApiRequests.Add(new ApiRequestMetric(method, requestUri, durationMs, statusCode, success));
        }

        public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed record ApiRequestMetric(HttpMethod Method, Uri? RequestUri, double DurationMs, int? StatusCode, bool Success);
}
