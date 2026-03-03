using System;

namespace MiniPainterHub.Server.Entities
{
    public class AppSetting
    {
        public string Key { get; set; } = default!;
        public string Value { get; set; } = default!;
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedByUserId { get; set; }
    }
}
