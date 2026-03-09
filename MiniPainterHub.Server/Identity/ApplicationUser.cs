using Microsoft.AspNetCore.Identity;
using MiniPainterHub.Server.Entities;
using System;
using System.Collections.Generic;

namespace MiniPainterHub.Server.Identity
{
    public class ApplicationUser : IdentityUser
    {
        public string? DisplayName { get; set; }

        public string? AvatarUrl { get; set; }

        public DateTime DateJoined { get; set; } = DateTime.UtcNow;

        public DateTime? SuspendedUntilUtc { get; set; }

        public string? SuspensionReason { get; set; }

        public DateTime? SuspensionUpdatedUtc { get; set; }

        public Profile? Profile { get; set; }

        public ICollection<Follow> Followers { get; set; } = new List<Follow>();

        public ICollection<Follow> Following { get; set; } = new List<Follow>();

        public ICollection<ConversationParticipant> ConversationParticipants { get; set; } = new List<ConversationParticipant>();

        public ICollection<DirectMessage> SentMessages { get; set; } = new List<DirectMessage>();
    }
}
