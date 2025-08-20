using System;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class CommentDto
    {
        public int Id { get; set; }
        public int PostId { get; set; }

        [Required]
        public string AuthorId { get; set; } = default!;

        [Required]
        [StringLength(100)]
        public string AuthorName { get; set; } = default!;

        [Required]
        [StringLength(4000)]
        public string Content { get; set; } = default!;

        public DateTime CreatedAt { get; set; }
    }
}
