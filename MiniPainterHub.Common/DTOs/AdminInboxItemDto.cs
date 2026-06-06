using System;

namespace MiniPainterHub.Common.DTOs
{
    public sealed class AdminInboxItemDto
    {
        public string TargetType { get; set; } = default!;
        public string TargetId { get; set; } = default!;
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public string AuthorUserId { get; set; } = default!;
        public string AuthorName { get; set; } = default!;
        public string Summary { get; set; } = default!;
        public string? ContextSummary { get; set; }
        public string TargetUrl { get; set; } = default!;
        public string State { get; set; } = default!;
        public bool IsDeleted { get; set; }
        public bool HasBeenReviewed { get; set; }
        public int OpenReportCount { get; set; }
        public int TotalReportCount { get; set; }
        public DateTime? LatestReportUtc { get; set; }
        public DateTime? ModeratedUtc { get; set; }
        public string? ModerationReason { get; set; }
    }
}
