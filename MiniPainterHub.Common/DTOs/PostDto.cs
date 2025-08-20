using System;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class PostDto
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = default!;

        [Required]
        [StringLength(4000)]
        public string Content { get; set; } = default!;

        [Required]
        public string CreatedById { get; set; } = default!;

        [Required]
        [StringLength(100)]
        public string AuthorName { get; set; } = default!;

        public DateTime CreatedAt { get; set; }

        [StringLength(2048)]
        public string? ImageUrl { get; set; }
    }
}
