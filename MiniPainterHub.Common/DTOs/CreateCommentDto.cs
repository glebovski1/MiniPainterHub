using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class CreateCommentDto
    {
        [Required]
        public int PostId { get; set; }

        [Required]
        [StringLength(4000)]
        public string Text { get; set; } = default!;
    }
}
