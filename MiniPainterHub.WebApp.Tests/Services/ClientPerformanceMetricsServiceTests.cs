using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Performance;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Services;

public class ClientPerformanceMetricsServiceTests
{
    [Fact]
    public async Task FlushAsync_WhenMetricIsRecorded_PostsSanitizedBatch()
    {
        var handler = new RecordingHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        var service = CreateService(handler);

        service.RecordMetric("blazor.first_render.ms", 42.5, "ms", "/home?tab=latest");

        await service.FlushAsync();

        handler.Requests.Should().ContainSingle();
        var request = handler.Requests.Single();
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri!.AbsolutePath.Should().Be("/api/client-performance");

        var batch = JsonSerializer.Deserialize<ClientPerformanceBatchDto>(request.Body!, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        batch.Should().NotBeNull();
        batch!.Metrics.Should().ContainSingle();
        batch.Metrics.Single().Name.Should().Be("blazor.first_render.ms");
        batch.Metrics.Single().Value.Should().Be(42.5);
        batch.Metrics.Single().Unit.Should().Be("ms");
        batch.Metrics.Single().Path.Should().Be("/home");
    }

    [Fact]
    public async Task FlushAsync_WhenPathHasEncodedQueryDelimiter_StripsEncodedQuery()
    {
        var handler = new RecordingHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        var service = CreateService(handler);

        service.RecordMetric("blazor.route_render.ms", 17.5, "ms", "/home%3Ftab=latest");

        await service.FlushAsync();

        var batch = JsonSerializer.Deserialize<ClientPerformanceBatchDto>(
            handler.Requests.Single().Body!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        batch!.Metrics.Single().Path.Should().Be("/home");
    }

    [Fact]
    public async Task FlushAsync_WhenApiRequestTargetsTelemetryEndpoint_DoesNotPostRecursiveMetric()
    {
        var handler = new RecordingHttpMessageHandler();
        var service = CreateService(handler);

        service.RecordApiRequest(
            HttpMethod.Post,
            new Uri("https://example.test/api/client-performance"),
            durationMs: 12,
            statusCode: 204,
            success: true);

        await service.FlushAsync();

        handler.Requests.Should().BeEmpty();
    }

    private static ClientPerformanceMetricsService CreateService(RecordingHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        };

        return new ClientPerformanceMetricsService(
            httpClient,
            new ClientPerformanceOptions
            {
                Enabled = true,
                SampleRate = 1.0,
                MaxBatchSize = 50
            });
    }
}
