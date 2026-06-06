using System;
using System.Collections.Generic;

namespace MiniPainterHub.Common.DTOs
{
    public sealed class AdminDashboardStatsDto
    {
        public DateTime GeneratedUtc { get; set; }
        public int WindowHours { get; set; }
        public long ActiveSessions { get; set; }
        public long PageViews { get; set; }
        public long NewPosts { get; set; }
        public long NewComments { get; set; }
        public long NewRegistrations { get; set; }
        public long ApiRequests { get; set; }
        public long ApiSuccessCount { get; set; }
        public long ApiErrorCount { get; set; }
        public double ApiSuccessRate { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public int SignalRConnections { get; set; }
        public long ImageUploadRequests { get; set; }
        public IReadOnlyList<AdminDashboardMetricDto> Metrics { get; set; } = Array.Empty<AdminDashboardMetricDto>();
        public IReadOnlyList<AdminDashboardActivityPointDto> Activity { get; set; } = Array.Empty<AdminDashboardActivityPointDto>();
        public IReadOnlyList<AdminDashboardHealthDto> Health { get; set; } = Array.Empty<AdminDashboardHealthDto>();
    }
}
