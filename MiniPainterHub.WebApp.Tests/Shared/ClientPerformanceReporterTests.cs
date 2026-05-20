using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Shared;
using MiniPainterHub.WebApp.Services.Performance;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Shared;

public class ClientPerformanceReporterTests : TestContext
{
    [Fact]
    public async Task Reporter_RecordsFirstRenderAndRouteRenderMetrics()
    {
        var metrics = new RecordingClientPerformanceMetrics();
        Services.AddSingleton<IClientPerformanceMetrics>(metrics);
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<ClientPerformanceReporter>();

        await WaitForMetricAsync(metrics, metric =>
            metric.Name == "blazor.first_render.ms"
            && metric.Path == "/");

        Services.GetRequiredService<NavigationManager>().NavigateTo("/home");
        cut.Render();

        await WaitForMetricAsync(metrics, metric =>
            metric.Name == "blazor.route_render.ms"
            && metric.Path == "/home");
    }

    private static async Task WaitForMetricAsync(
        RecordingClientPerformanceMetrics metrics,
        Func<ClientPerformanceMetricDto, bool> predicate)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (metrics.Recorded.Any(predicate))
            {
                return;
            }

            await Task.Delay(25);
        }

        metrics.Recorded.Any(predicate).Should().BeTrue();
    }

    private sealed class RecordingClientPerformanceMetrics : IClientPerformanceMetrics
    {
        public List<ClientPerformanceMetricDto> Recorded { get; } = new();

        public bool IsEnabled => true;

        public void EnableForSession()
        {
        }

        public void RecordMetric(string name, double value, string unit, string? path = null)
        {
            Recorded.Add(new ClientPerformanceMetricDto
            {
                Name = name,
                Value = value,
                Unit = unit,
                Path = path,
                CollectedAtUtc = DateTimeOffset.UtcNow
            });
        }

        public void RecordMetric(ClientPerformanceMetricDto metric)
        {
            Recorded.Add(metric);
        }

        public void RecordApiRequest(HttpMethod method, Uri? requestUri, double durationMs, int? statusCode, bool success)
        {
        }

        public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
