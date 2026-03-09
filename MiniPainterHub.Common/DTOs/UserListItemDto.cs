using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class UserListItemDto
    {
        [Required]
        public string UserId { get; set; } = default!;

        [Required]
        [StringLength(100)]
        public string UserName { get; set; } = default!;

        [Required]
        [StringLength(100)]
        public string DisplayName { get; set; } = default!;

        [StringLength(2048)]
        public string? AvatarUrl { get; set; }
    }
}
