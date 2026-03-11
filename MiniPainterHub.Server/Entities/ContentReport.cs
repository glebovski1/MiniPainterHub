using System;

namespace MiniPainterHub.Server.Entities
{
    public class ContentReport
    {
        public long Id { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public string ReporterUserId { get; set; } = default!;
        public string TargetType { get; set; } = default!;
        public string TargetId { get; set; } = default!;
        public string ReasonCode { get; set; } = default!;
        public string? Details { get; set; }
        public string Status { get; set; } = default!;
        public string? ReviewedByUserId { get; set; }
        public DateTime? ReviewedUtc { get; set; }
        public string? ResolutionNote { get; set; }
    }
}
