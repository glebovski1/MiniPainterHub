using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniPainterHub.Common.DTOs
{
    public class ProfileDto
    {
        /// <summary>
        /// The unique identifier of the user.
        /// </summary>
        public string UserId { get; set; } = default!;

        /// <summary>
        /// The user’s login name.
        /// </summary>
        public string UserName { get; set; } = default!;

        /// <summary>
        /// The user’s email address.
        /// </summary>
        public string Email { get; set; } = default!;

        /// <summary>
        /// The display name shown in the UI (optional).
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// URL pointing to the user’s avatar image (optional).
        /// </summary>
        public string? AvatarUrl { get; set; }

        /// <summary>
        /// A short biography or “about me” text (optional).
        /// </summary>
        public string? Bio { get; set; }

        /// <summary>
        /// When the user first registered.
        /// </summary>
        public DateTime DateJoined { get; set; }
    }
}
