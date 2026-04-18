using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class UpdateUserProfileDto
    {
        [Required]
        [StringLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Bio { get; set; } = string.Empty;

    }
}

