using System;

namespace MiniPainterHub.Server.Entities
{
    public class ModerationAuditLog
    {
        public long Id { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string ActorUserId { get; set; } = default!;
        public string ActorRole { get; set; } = default!;
        public string ActionType { get; set; } = default!;
        public string TargetType { get; set; } = default!;
        public string TargetId { get; set; } = default!;
        public string? Reason { get; set; }
        public string? MetadataJson { get; set; }
    }
}
