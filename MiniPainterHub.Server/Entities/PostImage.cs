using System;

namespace MiniPainterHub.Server.Entities
{
    public class PostImage
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public string ImageUrl { get; set; } = default!;
        public string? ThumbnailUrl { get; set; }
        // Navigation:
        public Post Post { get; set; } = default!;
    }
}
