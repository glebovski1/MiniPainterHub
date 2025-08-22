using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class CreatePostDto
    {
        [Required]
        [StringLength(100)]
        public string Title { get; set; } = default!;

        [Required]
        [StringLength(4000)]
        public string Content { get; set; } = default!;

        [MaxLength(5)]
        public List<PostImageDto>? Images { get; set; }
    }
}
