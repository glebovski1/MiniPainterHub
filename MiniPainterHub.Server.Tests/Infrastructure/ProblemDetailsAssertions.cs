using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;

namespace MiniPainterHub.Server.Tests.Infrastructure;

internal static class ProblemDetailsAssertions
{
    public static async Task AssertAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedTitle,
        string? expectedDetail = null,
        IEnumerable<string>? expectedErrorKeys = null,
        bool expectRequestId = true)
    {
        response.StatusCode.Should().Be(expectedStatus);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        root.GetProperty("title").GetString().Should().Be(expectedTitle);
        root.GetProperty("status").GetInt32().Should().Be((int)expectedStatus);

        if (!string.IsNullOrWhiteSpace(expectedDetail))
        {
            root.GetProperty("detail").GetString().Should().Be(expectedDetail);
        }

        if (expectRequestId)
        {
            root.TryGetProperty("requestId", out _).Should().BeTrue();
        }

        if (expectedErrorKeys is not null)
        {
            root.TryGetProperty("errors", out var errors).Should().BeTrue();
            var actualKeys = errors.EnumerateObject().Select(o => o.Name).ToArray();
            actualKeys.Should().Contain(expectedErrorKeys);
        }
    }
}
