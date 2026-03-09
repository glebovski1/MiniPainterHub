using MiniPainterHub.Server.Identity;
using System;

namespace MiniPainterHub.Server.Entities
{
    public class ConversationParticipant
    {
        public int ConversationId { get; set; }

        public string UserId { get; set; } = default!;

        public DateTime JoinedUtc { get; set; }

        public DateTime? LastReadMessageUtc { get; set; }

        public Conversation Conversation { get; set; } = default!;

        public ApplicationUser User { get; set; } = default!;
    }
}
