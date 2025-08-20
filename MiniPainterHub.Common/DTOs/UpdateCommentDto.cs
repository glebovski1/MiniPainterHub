using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class UpdateCommentDto
    {
        [Required]
        [StringLength(4000)]
        public string Content { get; set; }
    }
}
