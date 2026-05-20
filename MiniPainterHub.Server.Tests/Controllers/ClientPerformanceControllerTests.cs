using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public class ClientPerformanceControllerTests
{
    [Fact]
    public async Task Post_WhenAnonymousBatchIsValid_ReturnsNoContent()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/client-performance", new ClientPerformanceBatchDto
        {
            Metrics =
            {
                new ClientPerformanceMetricDto
                {
                    Name = "blazor.first_render.ms",
                    Value = 123.4,
                    Unit = "ms",
                    Path = "/home",
                    CollectedAtUtc = DateTimeOffset.UtcNow
                }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Post_WhenBatchContainsTooManyMetrics_ReturnsBadRequest()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var batch = new ClientPerformanceBatchDto();
        foreach (var index in Enumerable.Range(0, 51))
        {
            batch.Metrics.Add(new ClientPerformanceMetricDto
            {
                Name = $"blazor.metric.{index}",
                Value = index,
                Unit = "count",
                Path = "/",
                CollectedAtUtc = DateTimeOffset.UtcNow
            });
        }

        var response = await client.PostAsJsonAsync("/api/client-performance", batch);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_WhenMetricNameIsInvalid_ReturnsBadRequest()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/client-performance", new ClientPerformanceBatchDto
        {
            Metrics =
            {
                new ClientPerformanceMetricDto
                {
                    Name = "bad free text metric",
                    Value = 1,
                    Unit = "ms",
                    Path = "/",
                    CollectedAtUtc = DateTimeOffset.UtcNow
                }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
