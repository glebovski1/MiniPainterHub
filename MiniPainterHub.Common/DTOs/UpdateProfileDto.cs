using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class UpdateProfileDto
    {
        [Required]
        [StringLength(100)]
        public string DisplayName { get; set; }

        [Required]
        [StringLength(500)]
        public string Bio { get; set; }
        // add any other editable fields here
    }
}
