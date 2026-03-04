using System;

namespace MiniPainterHub.Common.DTOs
{
    public class ModerationPostPreviewDto
    {
        public int PostId { get; set; }
        public string Title { get; set; } = default!;
        public string ContentSnippet { get; set; } = default!;
        public string CreatedByUserId { get; set; } = default!;
        public bool IsDeleted { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public string? ModeratedByUserId { get; set; }
        public DateTime? ModeratedUtc { get; set; }
        public string? ModerationReason { get; set; }
    }
}
