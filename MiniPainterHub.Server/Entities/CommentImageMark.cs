using System;

namespace MiniPainterHub.Server.Entities
{
    public class CommentImageMark
    {
        public int CommentId { get; set; }

        public int PostImageId { get; set; }

        public decimal NormalizedX { get; set; }

        public decimal NormalizedY { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime UpdatedUtc { get; set; }

        public Comment Comment { get; set; } = default!;

        public PostImage PostImage { get; set; } = default!;
    }
}
