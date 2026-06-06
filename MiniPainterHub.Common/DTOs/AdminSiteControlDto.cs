using System;

namespace MiniPainterHub.Common.DTOs
{
    public sealed class AdminSiteControlDto
    {
        public string Key { get; set; } = default!;
        public string Label { get; set; } = default!;
        public string Description { get; set; } = default!;
        public bool Enabled { get; set; } = true;
        public bool EffectiveEnabled { get; set; } = true;
        public bool IsExpired { get; set; }
        public DateTime? DisabledUntilUtc { get; set; }
        public string? Message { get; set; }
        public string? Reason { get; set; }
        public DateTime? UpdatedUtc { get; set; }
        public string? UpdatedByUserId { get; set; }
    }
}
