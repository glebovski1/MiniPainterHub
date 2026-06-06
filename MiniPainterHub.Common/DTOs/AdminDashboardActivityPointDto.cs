using System;

namespace MiniPainterHub.Common.DTOs
{
    public sealed class AdminDashboardActivityPointDto
    {
        public DateTime TimestampUtc { get; set; }
        public long PageViews { get; set; }
        public long ApiRequests { get; set; }
        public long ApiErrors { get; set; }
    }
}
