using System;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class PublicUserProfileDto
    {
        [Required]
        public string UserId { get; set; } = default!;

        [Required]
        [StringLength(100)]
        public string UserName { get; set; } = default!;

        [StringLength(100)]
        public string? DisplayName { get; set; }

        [Url]
        [StringLength(2048)]
        public string? AvatarUrl { get; set; }

        [StringLength(500)]
        public string? Bio { get; set; }

        public DateTime DateJoined { get; set; }

        public int FollowerCount { get; set; }

        public int FollowingCount { get; set; }

        public bool IsFollowing { get; set; }

        public bool CanMessage { get; set; }
    }
}
