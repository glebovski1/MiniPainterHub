using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services;

public sealed class UploadConcurrencyLimiter : IUploadConcurrencyLimiter
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOptionsMonitor<TrafficShapingOptions> _options;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _clientLimiters = new(StringComparer.Ordinal);
    private SemaphoreSlim _globalLimiter;
    private int _globalLimit;

    public UploadConcurrencyLimiter(
        IHttpContextAccessor httpContextAccessor,
        IOptionsMonitor<TrafficShapingOptions> options)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _globalLimit = Math.Max(1, _options.CurrentValue.GlobalUploadConcurrency);
        _globalLimiter = new SemaphoreSlim(_globalLimit, _globalLimit);
    }

    public async ValueTask<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var global = GetGlobalLimiter(options);
        if (!await global.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            return null;
        }

        var key = ResolveClientKey(_httpContextAccessor.HttpContext);
        var perClientLimit = Math.Max(1, options.PerClientUploadConcurrency);
        var clientLimiter = _clientLimiters.GetOrAdd(key, _ => new SemaphoreSlim(perClientLimit, perClientLimit));
        if (!await clientLimiter.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            global.Release();
            return null;
        }

        return new Releaser(global, clientLimiter);
    }

    private SemaphoreSlim GetGlobalLimiter(TrafficShapingOptions options)
    {
        var configuredLimit = Math.Max(1, options.GlobalUploadConcurrency);
        if (configuredLimit == Volatile.Read(ref _globalLimit))
        {
            return _globalLimiter;
        }

        lock (_clientLimiters)
        {
            if (configuredLimit == _globalLimit)
            {
                return _globalLimiter;
            }

            _globalLimiter = new SemaphoreSlim(configuredLimit, configuredLimit);
            _globalLimit = configuredLimit;
            return _globalLimiter;
        }
    }

    private static string ResolveClientKey(HttpContext? context)
    {
        if (context is null)
        {
            return "no-http-context";
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return "user:" + userId;
        }

        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp is null)
        {
            return "ip:unknown";
        }

        if (remoteIp.IsIPv4MappedToIPv6)
        {
            remoteIp = remoteIp.MapToIPv4();
        }

        return "ip:" + remoteIp;
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private readonly SemaphoreSlim _global;
        private readonly SemaphoreSlim _client;
        private int _disposed;

        public Releaser(SemaphoreSlim global, SemaphoreSlim client)
        {
            _global = global;
            _client = client;
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _client.Release();
                _global.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
