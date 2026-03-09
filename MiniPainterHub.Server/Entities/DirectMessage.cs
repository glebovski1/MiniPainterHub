using MiniPainterHub.Server.Identity;
using System;

namespace MiniPainterHub.Server.Entities
{
    public class DirectMessage
    {
        public int Id { get; set; }

        public int ConversationId { get; set; }

        public string SenderUserId { get; set; } = default!;

        public string Body { get; set; } = default!;

        public DateTime SentUtc { get; set; }

        public Conversation Conversation { get; set; } = default!;

        public ApplicationUser SenderUser { get; set; } = default!;
    }
}
