using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MiniPainterHub.Common.DTOs;

namespace MiniPainterHub.WebApp.Services.Performance;

public sealed class ClientPerformanceMetricsService : IClientPerformanceMetrics
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private static readonly HashSet<string> AllowedUnits = new(StringComparer.Ordinal)
    {
        "ms",
        "count",
        "bytes",
        "score"
    };

    private readonly HttpClient _httpClient;
    private readonly ClientPerformanceOptions _options;
    private readonly object _sync = new();
    private readonly List<ClientPerformanceMetricDto> _pending = new();
    private readonly bool _sampledIn;
    private bool _sessionEnabled;
    private int _flushScheduled;

    public ClientPerformanceMetricsService(HttpClient httpClient, ClientPerformanceOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _sampledIn = _options.SampleRate >= 1 || (_options.SampleRate > 0 && Random.Shared.NextDouble() <= _options.SampleRate);
    }

    public bool IsEnabled => ShouldRecord();

    public void EnableForSession()
    {
        _sessionEnabled = true;
    }

    public void RecordMetric(string name, double value, string unit, string? path = null)
    {
        RecordMetric(new ClientPerformanceMetricDto
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
        if (!ShouldRecord() || !IsValidMetric(metric))
        {
            return;
        }

        metric.Path = SanitizePath(metric.Path);
        if (metric.CollectedAtUtc == default)
        {
            metric.CollectedAtUtc = DateTimeOffset.UtcNow;
        }

        lock (_sync)
        {
            if (_pending.Count >= EffectiveMaxBatchSize)
            {
                return;
            }

            _pending.Add(metric);
        }

        ScheduleFlush();
    }

    public void RecordApiRequest(HttpMethod method, Uri? requestUri, double durationMs, int? statusCode, bool success)
    {
        var path = SanitizePath(requestUri);
        if (path is null || string.Equals(path, "/api/client-performance", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        RecordMetric("api.request.duration.ms", durationMs, "ms", path);
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        List<ClientPerformanceMetricDto> batch;
        lock (_sync)
        {
            if (_pending.Count == 0)
            {
                return;
            }

            batch = _pending.Take(EffectiveMaxBatchSize).ToList();
            _pending.RemoveRange(0, batch.Count);
        }

        try
        {
            var payload = JsonSerializer.Serialize(new ClientPerformanceBatchDto { Metrics = batch }, SerializerOptions);
            using var content = new StringContent(payload, Encoding.UTF8);
            content.Headers.ContentType = new("application/json");
            using var response = await _httpClient.PostAsync("/api/client-performance", content, cancellationToken);
        }
        catch
        {
            // Metrics must never affect the user path.
        }
    }

    private int EffectiveMaxBatchSize => Math.Clamp(_options.MaxBatchSize, 1, 50);

    private bool ShouldRecord() => _sessionEnabled || (_options.Enabled && _sampledIn);

    private static bool IsValidMetric(ClientPerformanceMetricDto metric)
    {
        return metric is not null
            && !string.IsNullOrWhiteSpace(metric.Name)
            && metric.Name.Length <= 80
            && metric.Name.All(c => char.IsLower(c) || char.IsDigit(c) || c is '.' or '_' or '-')
            && !string.IsNullOrWhiteSpace(metric.Unit)
            && AllowedUnits.Contains(metric.Unit)
            && double.IsFinite(metric.Value)
            && metric.Value >= 0;
    }

    private static string? SanitizePath(Uri? uri)
    {
        if (uri is null)
        {
            return null;
        }

        return SanitizePath(uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString);
    }

    private static string? SanitizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        path = path.Trim();
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute)
            && (string.Equals(absolute.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(absolute.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            path = absolute.AbsolutePath;
        }

        var queryIndex = path.IndexOfAny(new[] { '?', '#' });
        if (queryIndex >= 0)
        {
            path = path[..queryIndex];
        }

        var encodedQueryIndex = path.IndexOf("%3F", StringComparison.OrdinalIgnoreCase);
        var encodedFragmentIndex = path.IndexOf("%23", StringComparison.OrdinalIgnoreCase);
        var encodedDelimiterIndex = encodedQueryIndex >= 0 && encodedFragmentIndex >= 0
            ? Math.Min(encodedQueryIndex, encodedFragmentIndex)
            : Math.Max(encodedQueryIndex, encodedFragmentIndex);
        if (encodedDelimiterIndex >= 0)
        {
            path = path[..encodedDelimiterIndex];
        }

        if (!path.StartsWith("/", StringComparison.Ordinal))
        {
            path = "/" + path.TrimStart('/');
        }

        return path.Length <= 256 ? path : path[..256];
    }

    private void ScheduleFlush()
    {
        if (Interlocked.Exchange(ref _flushScheduled, 1) == 1)
        {
            return;
        }

        _ = FlushLaterAsync();
    }

    private async Task FlushLaterAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            await FlushAsync();
        }
        finally
        {
            Interlocked.Exchange(ref _flushScheduled, 0);
        }
    }
}
