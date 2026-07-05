using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Infrastructure.RateLimiting;

namespace MiniPainterHub.Server.Controllers;

[ApiController]
[Route("api/client-performance")]
[AllowAnonymous]
public sealed class ClientPerformanceController : ControllerBase
{
    private const int MaxMetricsPerBatch = 50;
    private static readonly Regex MetricNamePattern = new("^[a-z0-9_.-]{1,80}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> AllowedUnits = new(StringComparer.Ordinal)
    {
        "ms",
        "count",
        "bytes",
        "score"
    };

    private readonly ILogger<ClientPerformanceController> _logger;

    public ClientPerformanceController(ILogger<ClientPerformanceController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitingPolicies.Write)]
    public IActionResult Post([FromBody] ClientPerformanceBatchDto batch)
    {
        ValidateBatch(batch);

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        foreach (var metric in batch.Metrics)
        {
            _logger.LogInformation(
                "ClientPerformanceMetric {MetricName}={MetricValue} {MetricUnit} Path={MetricPath}",
                metric.Name,
                metric.Value,
                metric.Unit,
                metric.Path);
        }

        return NoContent();
    }

    private void ValidateBatch(ClientPerformanceBatchDto? batch)
    {
        if (batch?.Metrics is null)
        {
            ModelState.AddModelError(nameof(ClientPerformanceBatchDto.Metrics), "Metrics are required.");
            return;
        }

        if (batch.Metrics.Count > MaxMetricsPerBatch)
        {
            ModelState.AddModelError(nameof(ClientPerformanceBatchDto.Metrics), $"At most {MaxMetricsPerBatch} metrics can be submitted in one batch.");
        }

        for (var index = 0; index < batch.Metrics.Count; index++)
        {
            var metric = batch.Metrics[index];
            var prefix = $"metrics[{index}]";

            if (string.IsNullOrWhiteSpace(metric.Name) || !MetricNamePattern.IsMatch(metric.Name))
            {
                ModelState.AddModelError($"{prefix}.name", "Metric name must contain only lowercase letters, digits, dots, underscores, or dashes.");
            }

            if (string.IsNullOrWhiteSpace(metric.Unit) || !AllowedUnits.Contains(metric.Unit))
            {
                ModelState.AddModelError($"{prefix}.unit", "Metric unit is not supported.");
            }

            if (!double.IsFinite(metric.Value) || metric.Value < 0)
            {
                ModelState.AddModelError($"{prefix}.value", "Metric value must be a finite non-negative number.");
            }

            if (metric.Path is { Length: > 0 } path
                && (!path.StartsWith("/", StringComparison.Ordinal) || path.Contains("?", StringComparison.Ordinal)))
            {
                ModelState.AddModelError($"{prefix}.path", "Metric path must be a query-free absolute path.");
            }
        }
    }
}
