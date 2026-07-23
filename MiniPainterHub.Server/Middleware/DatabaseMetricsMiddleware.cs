using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Infrastructure.Database;
using MiniPainterHub.Server.Options;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Middleware;

public sealed class DatabaseMetricsMiddleware
{
    private static readonly Meter Meter = new("MiniPainterHub.Server.Database");
    private static readonly Histogram<int> CommandsPerRequest =
        Meter.CreateHistogram<int>("minipainterhub.database.commands_per_request");
    private static readonly Histogram<double> DatabaseMillisecondsPerRequest =
        Meter.CreateHistogram<double>("minipainterhub.database.duration_per_request", unit: "ms");

    private readonly RequestDelegate _next;
    private readonly ILogger<DatabaseMetricsMiddleware> _logger;
    private readonly DatabasePerformanceOptions _options;

    public DatabaseMetricsMiddleware(
        RequestDelegate next,
        ILogger<DatabaseMetricsMiddleware> logger,
        IOptions<DatabasePerformanceOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context, RequestDatabaseMetrics requestMetrics)
    {
        await _next(context);

        if (!_options.Enabled)
        {
            return;
        }

        var snapshot = requestMetrics.Snapshot();
        if (snapshot.CommandCount == 0)
        {
            return;
        }

        var endpoint = context.GetEndpoint()?.DisplayName ?? "unmatched";
        var tags = new TagList
        {
            { "http.route", endpoint },
            { "http.response.status_code", context.Response.StatusCode }
        };
        CommandsPerRequest.Record(snapshot.CommandCount, tags);
        DatabaseMillisecondsPerRequest.Record(snapshot.DatabaseDuration.TotalMilliseconds, tags);

        if (snapshot.CommandCount > _options.ExcessiveCommandCount
            || snapshot.DatabaseDuration.TotalMilliseconds > _options.SlowRequestDatabaseMilliseconds)
        {
            _logger.LogWarning(
                "Database-heavy request. Endpoint={Endpoint}; StatusCode={StatusCode}; CommandCount={CommandCount}; DatabaseMilliseconds={DatabaseMilliseconds:F1}.",
                endpoint,
                context.Response.StatusCode,
                snapshot.CommandCount,
                snapshot.DatabaseDuration.TotalMilliseconds);
        }
    }
}
