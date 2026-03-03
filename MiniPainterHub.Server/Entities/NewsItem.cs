using System;

namespace MiniPainterHub.Server.Entities
{
    public class NewsItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = default!;
        public string BodyMarkdown { get; set; } = default!;
        public DateTime PublishAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsPinned { get; set; }
        public int PinPriority { get; set; }
        public ContentStatus Status { get; set; } = ContentStatus.Active;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
