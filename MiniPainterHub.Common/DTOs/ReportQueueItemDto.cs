using System;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public sealed class ReportQueueItemDto
    {
        public long Id { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? ReviewedUtc { get; set; }

        [Required]
        [StringLength(64)]
        public string Status { get; set; } = default!;

        [Required]
        [StringLength(64)]
        public string TargetType { get; set; } = default!;

        [Required]
        [StringLength(128)]
        public string TargetId { get; set; } = default!;

        [Required]
        [StringLength(32)]
        public string ReasonCode { get; set; } = default!;

        [StringLength(1000)]
        public string? Details { get; set; }

        [Required]
        [StringLength(450)]
        public string ReporterUserId { get; set; } = default!;

        [Required]
        [StringLength(100)]
        public string ReporterUserName { get; set; } = default!;

        [StringLength(450)]
        public string? ReviewedByUserId { get; set; }

        [StringLength(100)]
        public string? ReviewedByUserName { get; set; }

        [Required]
        [StringLength(256)]
        public string TargetSummary { get; set; } = default!;

        [Required]
        [StringLength(256)]
        public string TargetUrl { get; set; } = default!;

        [StringLength(500)]
        public string? ResolutionNote { get; set; }
    }
}
