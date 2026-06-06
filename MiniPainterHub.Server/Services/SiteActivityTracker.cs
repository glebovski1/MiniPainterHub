using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MiniPainterHub.Server.Services
{
    public sealed class SiteActivityTracker : ISiteActivityTracker
    {
        private static readonly TimeSpan ActiveSessionWindow = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan RetentionWindow = TimeSpan.FromHours(48);

        private readonly ConcurrentQueue<SiteActivityRequest> _requests = new();
        private readonly ConcurrentDictionary<string, DateTime> _sessions = new(StringComparer.Ordinal);
        private int _signalRConnections;

        public void RecordRequest(SiteActivityRequest request)
        {
            _requests.Enqueue(request);
            if (!string.IsNullOrWhiteSpace(request.SessionKey))
            {
                _sessions.AddOrUpdate(request.SessionKey, request.TimestampUtc, (_, _) => request.TimestampUtc);
            }

            Prune(request.TimestampUtc);
        }

        public void RecordSignalRConnected() =>
            Interlocked.Increment(ref _signalRConnections);

        public void RecordSignalRDisconnected()
        {
            var updated = Interlocked.Decrement(ref _signalRConnections);
            if (updated < 0)
            {
                Interlocked.Exchange(ref _signalRConnections, 0);
            }
        }

        public SiteActivitySnapshot GetSnapshot(TimeSpan window)
        {
            var now = DateTime.UtcNow;
            Prune(now);

            var cutoff = now.Subtract(window);
            var requests = _requests.Where(r => r.TimestampUtc >= cutoff).ToList();
            var apiRequests = requests.Where(r => r.IsApi).ToList();
            var success = apiRequests.Count(r => r.StatusCode is >= 200 and < 400);
            var errors = apiRequests.Count - success;
            var averageResponseMs = apiRequests.Count == 0 ? 0 : apiRequests.Average(r => r.DurationMs);

            return new SiteActivitySnapshot(
                ActiveSessions: _sessions.Values.Count(lastSeen => lastSeen >= now.Subtract(ActiveSessionWindow)),
                PageViews: requests.LongCount(r => r.IsPageView),
                ApiRequests: apiRequests.Count,
                ApiSuccessCount: success,
                ApiErrorCount: errors,
                AverageResponseTimeMs: averageResponseMs,
                SignalRConnections: Volatile.Read(ref _signalRConnections),
                ImageUploadRequests: requests.LongCount(r => r.IsImageUpload),
                Activity: BuildActivity(requests, window, now));
        }

        private static IReadOnlyList<SiteActivityPoint> BuildActivity(IReadOnlyList<SiteActivityRequest> requests, TimeSpan window, DateTime now)
        {
            var bucket = window.TotalHours <= 6
                ? TimeSpan.FromMinutes(5)
                : window.TotalHours <= 24
                    ? TimeSpan.FromHours(1)
                    : TimeSpan.FromHours(6);
            var bucketCount = Math.Clamp((int)Math.Ceiling(window.TotalMilliseconds / bucket.TotalMilliseconds), 1, 48);
            var start = Floor(now.Subtract(window), bucket);
            var points = new List<SiteActivityPoint>(bucketCount);

            for (var i = 0; i < bucketCount; i++)
            {
                var bucketStart = start.AddMilliseconds(bucket.TotalMilliseconds * i);
                var bucketEnd = bucketStart.Add(bucket);
                var bucketRequests = requests.Where(r => r.TimestampUtc >= bucketStart && r.TimestampUtc < bucketEnd).ToList();
                points.Add(new SiteActivityPoint(
                    bucketStart,
                    bucketRequests.LongCount(r => r.IsPageView),
                    bucketRequests.LongCount(r => r.IsApi),
                    bucketRequests.LongCount(r => r.IsApi && (r.StatusCode < 200 || r.StatusCode >= 400))));
            }

            return points;
        }

        private void Prune(DateTime now)
        {
            var requestCutoff = now.Subtract(RetentionWindow);
            while (_requests.TryPeek(out var oldest) && oldest.TimestampUtc < requestCutoff)
            {
                _requests.TryDequeue(out _);
            }

            var sessionCutoff = now.Subtract(ActiveSessionWindow);
            foreach (var session in _sessions)
            {
                if (session.Value < sessionCutoff)
                {
                    _sessions.TryRemove(session.Key, out _);
                }
            }
        }

        private static DateTime Floor(DateTime value, TimeSpan bucket)
        {
            var ticks = value.Ticks - value.Ticks % bucket.Ticks;
            return new DateTime(ticks, DateTimeKind.Utc);
        }
    }
}
