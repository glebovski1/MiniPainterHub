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

public class CommentServiceQueryTests
{
    [Fact]
    public async Task GetByPostAsync_WhenVisibilityFlagsProvided_AppendsFlagsToQuery()
    {
        var handler = new CapturingHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var apiClient = new ApiClient(httpClient, new NoOpNotificationService());
        var service = new CommentService(apiClient);

        var result = await service.GetByPostAsync(postId: 12, page: 2, pageSize: 10, includeDeleted: true, deletedOnly: true);

        result.Success.Should().BeTrue();
        var query = handler.LastRequestUri?.Query;
        query.Should().NotBeNull();
        query.Should().Contain("page=2");
        query.Should().Contain("pageSize=10");
        query.Should().Contain("includeDeleted=True");
        query.Should().Contain("deletedOnly=True");
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private static readonly string EmptyPagedResultJson = "{\"items\":[],\"totalCount\":0,\"pageNumber\":1,\"pageSize\":10}";

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
