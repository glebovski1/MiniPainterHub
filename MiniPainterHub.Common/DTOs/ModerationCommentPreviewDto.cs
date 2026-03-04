using System;

namespace MiniPainterHub.Common.DTOs
{
    public class ModerationCommentPreviewDto
    {
        public int CommentId { get; set; }
        public int PostId { get; set; }
        public string AuthorUserId { get; set; } = default!;
        public string TextSnippet { get; set; } = default!;
        public bool IsDeleted { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public string? ModeratedByUserId { get; set; }
        public DateTime? ModeratedUtc { get; set; }
        public string? ModerationReason { get; set; }
    }
}
