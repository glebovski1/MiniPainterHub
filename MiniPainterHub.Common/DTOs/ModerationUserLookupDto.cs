using System;
using System.Collections.Generic;

namespace MiniPainterHub.Common.DTOs
{
    public class ModerationUserLookupDto
    {
        public string UserId { get; set; } = default!;
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public bool IsSuspended { get; set; }
        public DateTime? SuspendedUntilUtc { get; set; }
        public string? SuspensionReason { get; set; }
        public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    }
}
