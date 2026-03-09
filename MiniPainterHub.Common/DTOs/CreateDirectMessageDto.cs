using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class CreateDirectMessageDto
    {
        [Required]
        [StringLength(2000)]
        public string Body { get; set; } = default!;
    }
}
