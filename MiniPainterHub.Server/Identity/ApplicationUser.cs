using Microsoft.AspNetCore.Identity;
using MiniPainterHub.Server.Entities;
using System;

namespace MiniPainterHub.Server.Identity
{
    public class ApplicationUser : IdentityUser
    {
        public string? DisplayName { get; set; }

        public string? AvatarUrl { get; set; }

        public DateTime DateJoined { get; set; } = DateTime.UtcNow;

        public Profile? Profile { get; set; }
    }
}
