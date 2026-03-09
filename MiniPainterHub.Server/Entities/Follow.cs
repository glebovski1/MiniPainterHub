using MiniPainterHub.Server.Identity;
using System;

namespace MiniPainterHub.Server.Entities
{
    public class Follow
    {
        public string FollowerUserId { get; set; } = default!;

        public string FollowedUserId { get; set; } = default!;

        public DateTime CreatedUtc { get; set; }

        public ApplicationUser FollowerUser { get; set; } = default!;

        public ApplicationUser FollowedUser { get; set; } = default!;
    }
}
