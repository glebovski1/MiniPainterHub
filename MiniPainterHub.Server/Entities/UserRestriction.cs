using System;

namespace MiniPainterHub.Server.Entities
{
    public class UserRestriction
    {
        public string UserId { get; set; } = default!;
        public bool IsSuspended { get; set; }
        public bool CanPost { get; set; } = true;
        public bool CanComment { get; set; } = true;
        public bool CanPostImages { get; set; } = true;
        public string? Reason { get; set; }
        public DateTime? Until { get; set; }
    }
}
