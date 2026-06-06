using System;
using System.Collections.Generic;

namespace MiniPainterHub.Common.DTOs
{
    public sealed class AdminInboxDetailDto
    {
        public string TargetType { get; set; } = default!;
        public string TargetId { get; set; } = default!;
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public string AuthorUserId { get; set; } = default!;
        public string AuthorName { get; set; } = default!;
        public string? AuthorEmail { get; set; }
        public DateTime? AuthorJoinedUtc { get; set; }
        public bool AuthorSuspended { get; set; }
        public DateTime? AuthorSuspendedUntilUtc { get; set; }
        public string? Title { get; set; }
        public string Body { get; set; } = default!;
        public string? ContextSummary { get; set; }
        public int? ParentPostId { get; set; }
        public string? ParentPostTitle { get; set; }
        public string TargetUrl { get; set; } = default!;
        public string AuditUrl { get; set; } = default!;
        public string State { get; set; } = default!;
        public bool IsDeleted { get; set; }
        public bool HasBeenReviewed { get; set; }
        public DateTime? ModeratedUtc { get; set; }
        public string? ModeratedByUserId { get; set; }
        public string? ModerationReason { get; set; }
        public IReadOnlyList<AdminInboxReportDto> Reports { get; set; } = Array.Empty<AdminInboxReportDto>();
    }
}
