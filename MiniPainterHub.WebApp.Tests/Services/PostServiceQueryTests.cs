using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MiniPainterHub.WebApp.Services;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Notifications;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Services;

public class PostServiceQueryTests
{
    [Fact]
    public async Task GetAllAsync_WhenVisibilityFlagsProvided_AppendsFlagsToQuery()
    {
        var handler = new CapturingHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var apiClient = new ApiClient(httpClient, new NoOpNotificationService());
        var service = new PostService(apiClient);

        var result = await service.GetAllAsync(page: 2, pageSize: 9, includeDeleted: true, deletedOnly: true);

        result.Success.Should().BeTrue();
        var query = handler.LastRequestUri?.Query;
        query.Should().NotBeNull();
        query.Should().Contain("page=2");
        query.Should().Contain("pageSize=9");
        query.Should().Contain("includeDeleted=True");
        query.Should().Contain("deletedOnly=True");
    }

    [Fact]
    public async Task GetTopPosts_UsesDedicatedTopPostsEndpoint()
    {
        var handler = new CapturingHttpMessageHandler("""[]""");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var apiClient = new ApiClient(httpClient, new NoOpNotificationService());
        var service = new PostService(apiClient);

        await service.GetTopPosts(5, TimeSpan.FromDays(30));

        handler.LastRequestUri.Should().NotBeNull();
        handler.LastRequestUri!.AbsolutePath.Should().Be("/api/posts/top");
        handler.LastRequestUri.Query.Should().Contain("count=5");
        handler.LastRequestUri.Query.Should().Contain("lookbackDays=30");
        handler.LastRequestUri.Query.Should().NotContain("pageSize=1000");
        handler.LastRequestUri.Query.Should().NotContain("pagesize=1000");
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseJson;

        public CapturingHttpMessageHandler(string responseJson = "{\"items\":[],\"totalCount\":0,\"pageNumber\":1,\"pageSize\":9}")
        {
            _responseJson = responseJson;
        }

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
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
