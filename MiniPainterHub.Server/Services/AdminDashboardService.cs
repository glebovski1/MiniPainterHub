using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public sealed class AdminDashboardService : IAdminDashboardService
    {
        private const int MinWindowHours = 1;
        private const int MaxWindowHours = 24 * 30;

        private readonly AppDbContext _db;
        private readonly ISiteActivityTracker _activityTracker;

        public AdminDashboardService(AppDbContext db, ISiteActivityTracker activityTracker)
        {
            _db = db;
            _activityTracker = activityTracker;
        }

        public async Task<AdminDashboardStatsDto> GetStatsAsync(int windowHours)
        {
            var safeWindowHours = Math.Clamp(windowHours, MinWindowHours, MaxWindowHours);
            var window = TimeSpan.FromHours(safeWindowHours);
            var cutoff = DateTime.UtcNow.Subtract(window);
            var runtime = _activityTracker.GetSnapshot(window);

            var newPosts = await _db.Posts.AsNoTracking().LongCountAsync(p => p.CreatedUtc >= cutoff);
            var newComments = await _db.Comments.AsNoTracking().LongCountAsync(c => c.CreatedUtc >= cutoff);
            var newRegistrations = await _db.Users.AsNoTracking().LongCountAsync(u => u.DateJoined >= cutoff);
            var successRate = runtime.ApiRequests == 0
                ? 1
                : runtime.ApiSuccessCount / (double)runtime.ApiRequests;

            var stats = new AdminDashboardStatsDto
            {
                GeneratedUtc = DateTime.UtcNow,
                WindowHours = safeWindowHours,
                ActiveSessions = runtime.ActiveSessions,
                PageViews = runtime.PageViews,
                NewPosts = newPosts,
                NewComments = newComments,
                NewRegistrations = newRegistrations,
                ApiRequests = runtime.ApiRequests,
                ApiSuccessCount = runtime.ApiSuccessCount,
                ApiErrorCount = runtime.ApiErrorCount,
                ApiSuccessRate = successRate,
                AverageResponseTimeMs = runtime.AverageResponseTimeMs,
                SignalRConnections = runtime.SignalRConnections,
                ImageUploadRequests = runtime.ImageUploadRequests,
                Activity = runtime.Activity
                    .Select(p => new AdminDashboardActivityPointDto
                    {
                        TimestampUtc = p.TimestampUtc,
                        PageViews = p.PageViews,
                        ApiRequests = p.ApiRequests,
                        ApiErrors = p.ApiErrors
                    })
                    .ToList()
            };

            stats.Metrics = BuildMetrics(stats);
            stats.Health = BuildHealth(stats);
            return stats;
        }

        private static IReadOnlyList<AdminDashboardMetricDto> BuildMetrics(AdminDashboardStatsDto stats)
        {
            var culture = CultureInfo.InvariantCulture;
            return new List<AdminDashboardMetricDto>
            {
                Metric("activeSessions", "Active sessions", stats.ActiveSessions.ToString("N0", culture)),
                Metric("pageViews", "Page views", stats.PageViews.ToString("N0", culture)),
                Metric("newPosts", "New posts", stats.NewPosts.ToString("N0", culture)),
                Metric("newComments", "New comments", stats.NewComments.ToString("N0", culture)),
                Metric("newRegistrations", "Registrations", stats.NewRegistrations.ToString("N0", culture)),
                Metric("apiSuccess", "API success", (stats.ApiSuccessRate * 100).ToString("0.0", culture), "%", stats.ApiSuccessRate >= 0.95 ? "Normal" : "Watch"),
                Metric("responseTime", "Avg response", stats.AverageResponseTimeMs.ToString("0", culture), "ms", stats.AverageResponseTimeMs <= 500 ? "Normal" : "Watch"),
                Metric("signalR", "SignalR connections", stats.SignalRConnections.ToString("N0", culture)),
                Metric("imageUploads", "Image upload requests", stats.ImageUploadRequests.ToString("N0", culture))
            };
        }

        private static IReadOnlyList<AdminDashboardHealthDto> BuildHealth(AdminDashboardStatsDto stats)
        {
            var apiStatus = stats.ApiRequests == 0 || stats.ApiSuccessRate >= 0.95 ? "Healthy" : "Degraded";
            var responseStatus = stats.AverageResponseTimeMs <= 500 ? "Healthy" : "Watch";
            return new List<AdminDashboardHealthDto>
            {
                new()
                {
                    Key = "api",
                    Label = "API",
                    Status = apiStatus,
                    Detail = stats.ApiRequests == 0
                        ? "No API traffic in this window."
                        : $"{stats.ApiSuccessCount} successful, {stats.ApiErrorCount} errored."
                },
                new()
                {
                    Key = "responseTime",
                    Label = "Response time",
                    Status = responseStatus,
                    Detail = $"{stats.AverageResponseTimeMs:0} ms average API response."
                },
                new()
                {
                    Key = "images",
                    Label = "Image uploads",
                    Status = "Available",
                    Detail = $"{stats.ImageUploadRequests} upload endpoint requests in this window."
                },
                new()
                {
                    Key = "backgroundJobs",
                    Label = "Background jobs",
                    Status = "Idle",
                    Detail = "No dedicated background job worker is configured in v1."
                }
            };
        }

        private static AdminDashboardMetricDto Metric(string key, string label, string value, string? unit = null, string status = "Normal") =>
            new()
            {
                Key = key,
                Label = label,
                Value = value,
                Unit = unit,
                Status = status
            };
    }
}
