using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class CreateUserProfileDto
    {
        [Required]
        [StringLength(100)]
        public string DisplayName { get; set; }

        [Required]
        [StringLength(500)]
        public string Bio { get; set; }
    }
}
