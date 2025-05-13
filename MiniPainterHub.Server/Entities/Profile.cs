using MiniPainterHub.Server.Identity;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Server.Entities
{
    public class Profile
    {
        [Key]
        [ForeignKey(nameof(User))]
        public string UserId { get; set; }      // PK, also FK to ApplicationUser
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Bio { get; set; }
        // Navigation:
        public ApplicationUser User { get; set; }
    }
}
