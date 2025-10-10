using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class PostImageDto
    {
        public int Id { get; set; }

        [Required]
        [StringLength(2048)]
        public string ImageUrl { get; set; } = default!;

        [StringLength(2048)]
        public string? PreviewUrl { get; set; }

        [StringLength(2048)]
        public string? ThumbnailUrl { get; set; }
    }
}
