using System;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class ProfileDto
    {
        /// <summary>
        /// The unique identifier of the user.
        /// </summary>
        [Required]
        public string UserId { get; set; } = default!;

        /// <summary>
        /// The user’s login name.
        /// </summary>
        [Required]
        [StringLength(100)]
        public string UserName { get; set; } = default!;

        /// <summary>
        /// The user’s email address.
        /// </summary>
        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = default!;

        /// <summary>
        /// The display name shown in the UI (optional).
        /// </summary>
        [StringLength(100)]
        public string? DisplayName { get; set; }

        /// <summary>
        /// URL pointing to the user’s avatar image (optional).
        /// </summary>
        [Url]
        [StringLength(2048)]
        public string? AvatarUrl { get; set; }

        /// <summary>
        /// A short biography or “about me” text (optional).
        /// </summary>
        [StringLength(500)]
        public string? Bio { get; set; }

        /// <summary>
        /// When the user first registered.
        /// </summary>
        public DateTime DateJoined { get; set; }
    }
}
