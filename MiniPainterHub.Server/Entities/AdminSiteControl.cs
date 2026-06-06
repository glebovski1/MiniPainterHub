using System;

namespace MiniPainterHub.Server.Entities
{
    public sealed class AdminSiteControl
    {
        public string Key { get; set; } = default!;
        public bool Enabled { get; set; } = true;
        public DateTime? DisabledUntilUtc { get; set; }
        public string? Message { get; set; }
        public string? Reason { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public string UpdatedByUserId { get; set; } = default!;
    }
}
