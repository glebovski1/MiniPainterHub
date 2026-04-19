using System;
using System.Collections.Generic;

namespace MiniPainterHub.Server.Entities
{
    public class PostImage
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public string ImageUrl { get; set; } = default!;
        public string? PreviewUrl { get; set; }
        public string? ThumbnailUrl { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public Guid? StoredImageId { get; set; }
        public string? ImageStorageKey { get; set; }
        public string? ThumbnailStorageKey { get; set; }
        // Navigation:
        public Post Post { get; set; } = default!;
        public List<ImageAuthorMark> AuthorMarks { get; set; } = new();
        public List<CommentImageMark> CommentMarks { get; set; } = new();
    }
}
