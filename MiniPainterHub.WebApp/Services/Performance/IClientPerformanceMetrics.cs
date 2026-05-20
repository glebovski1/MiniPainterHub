using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MiniPainterHub.Common.DTOs;

namespace MiniPainterHub.WebApp.Services.Performance;

public interface IClientPerformanceMetrics
{
    bool IsEnabled { get; }

    void EnableForSession();

    void RecordMetric(string name, double value, string unit, string? path = null);

    void RecordMetric(ClientPerformanceMetricDto metric);

    void RecordApiRequest(HttpMethod method, Uri? requestUri, double durationMs, int? statusCode, bool success);

    Task FlushAsync(CancellationToken cancellationToken = default);
}
