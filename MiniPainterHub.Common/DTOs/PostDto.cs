using System;
using System.Collections.Generic;
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

        [StringLength(120)]
        public string? MiniatureName { get; set; }

        [StringLength(2000)]
        public string? PaintsUsed { get; set; }

        [StringLength(1000)]
        public string? Techniques { get; set; }

        [StringLength(40)]
        public string? Difficulty { get; set; }

        [StringLength(80)]
        public string? TimeSpent { get; set; }

        [Required]
        public string CreatedById { get; set; } = default!;

        [Required]
        [StringLength(100)]
        public string AuthorName { get; set; } = default!;

        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Convenience URL for the first image associated with the post, if any.
        /// Helps legacy clients that expect a single image field.
        /// </summary>
        public string? ImageUrl { get; set; }

        public List<PostImageDto> Images { get; set; } = new();

        public List<TagDto> Tags { get; set; } = new();

        public HobbyProjectReferenceDto? Project { get; set; }
    }
}
