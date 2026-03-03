using System;

namespace MiniPainterHub.Server.Entities
{
    public class ModerationAction
    {
        public long Id { get; set; }
        public string ActorUserId { get; set; } = default!;
        public string Action { get; set; } = default!;
        public string TargetType { get; set; } = default!;
        public string TargetId { get; set; } = default!;
        public string? Reason { get; set; }
        public string? OldValueJson { get; set; }
        public string? NewValueJson { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
