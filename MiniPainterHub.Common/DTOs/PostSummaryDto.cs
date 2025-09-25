using System;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class PostSummaryDto
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = default!;

        [Required]
        [StringLength(100)]
        public string Snippet { get; set; } = default!;   // e.g. first 100 chars

        [StringLength(2048)]
        public string? ImageUrl { get; set; }

        [Required]
        [StringLength(100)]
        public string AuthorName { get; set; } = default!;

        public string AuthorId { get; set; } = default!;

        public DateTime CreatedAt { get; set; }
        public int CommentCount { get; set; }
        public int LikeCount { get; set; }
    }
}
