using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Notifications;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Services;

public class ConversationServiceTests
{
    [Fact]
    public async Task GetConversationsAsync_WhenConcurrentRefreshesOverlap_SendsSingleRequest()
    {
        using var context = new TestContext();
        var handler = new BlockingHttpMessageHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var apiClient = new ApiClient(httpClient, new NoOpNotificationService());
        var service = new ConversationService(
            apiClient,
            context.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>(),
            context.JSInterop.JSRuntime);

        var firstTask = service.GetConversationsAsync(forceRefresh: true);
        await handler.RequestStarted.Task;

        var secondTask = service.GetConversationsAsync(forceRefresh: true);
        handler.ReleaseResponse.TrySetResult();

        var results = await Task.WhenAll(firstTask, secondTask);

        handler.RequestCount.Should().Be(1);
        results[0].Should().HaveCount(1);
        results[1].Should().HaveCount(1);
        results[0][0].OtherUser.DisplayName.Should().Be("Other Painter");
    }

    private sealed class BlockingHttpMessageHandler : HttpMessageHandler
    {
        private const string ConversationsJson =
            "[{\"id\":1,\"otherUser\":{\"userId\":\"other-user\",\"userName\":\"other\",\"displayName\":\"Other Painter\"},\"latestMessagePreview\":\"Hi\",\"latestMessageSentUtc\":\"2026-03-11T12:00:00Z\",\"unreadCount\":1}]";

        public TaskCompletionSource RequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseResponse { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestStarted.TrySetResult();
            await ReleaseResponse.Task.WaitAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ConversationsJson, Encoding.UTF8, "application/json")
            };
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
