using System;

namespace MiniPainterHub.Common.DTOs
{
    public sealed class AdminInboxReportDto
    {
        public long Id { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? ReviewedUtc { get; set; }
        public string Status { get; set; } = default!;
        public string ReasonCode { get; set; } = default!;
        public string? Details { get; set; }
        public string ReporterUserId { get; set; } = default!;
        public string ReporterUserName { get; set; } = default!;
        public string? ReviewedByUserId { get; set; }
        public string? ResolutionNote { get; set; }
    }
}
