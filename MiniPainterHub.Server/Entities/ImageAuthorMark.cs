using MiniPainterHub.Server.Identity;
using System;

namespace MiniPainterHub.Server.Entities
{
    public class ImageAuthorMark
    {
        public int Id { get; set; }

        public int PostImageId { get; set; }

        public string CreatedByUserId { get; set; } = default!;

        public decimal NormalizedX { get; set; }

        public decimal NormalizedY { get; set; }

        public string? Tag { get; set; }

        public string? Message { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime UpdatedUtc { get; set; }

        public PostImage PostImage { get; set; } = default!;

        public ApplicationUser CreatedByUser { get; set; } = default!;
    }
}
