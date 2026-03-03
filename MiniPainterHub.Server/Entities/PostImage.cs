using System;

namespace MiniPainterHub.Server.Entities
{
    public class PostImage
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public string ImageUrl { get; set; } = default!;
        public string? PreviewUrl { get; set; }
        public string? ThumbnailUrl { get; set; }
        public ContentStatus Status { get; set; } = ContentStatus.Active;
        public string? ModerationNote { get; set; }
        public DateTime? ModeratedAt { get; set; }
        public string? ModeratedByUserId { get; set; }
        public DateTime? DeletedAt { get; set; }
        // Navigation:
        public Post Post { get; set; } = default!;
    }
}
