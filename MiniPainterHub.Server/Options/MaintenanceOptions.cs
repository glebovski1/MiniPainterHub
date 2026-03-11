using System;

namespace MiniPainterHub.Server.Options
{
    public sealed class MaintenanceOptions
    {
        public bool Enabled { get; set; }
        public string? Message { get; set; }
        public DateTime? PlannedEndUtc { get; set; }
        public bool AllowAdmins { get; set; } = true;
    }
}
