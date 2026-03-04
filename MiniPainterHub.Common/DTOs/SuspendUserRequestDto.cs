using System;

namespace MiniPainterHub.Common.DTOs
{
    public class SuspendUserRequestDto
    {
        public DateTime? SuspendedUntilUtc { get; set; }
        public string? Reason { get; set; }
    }
}
