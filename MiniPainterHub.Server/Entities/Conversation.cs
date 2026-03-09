using System;
using System.Collections.Generic;

namespace MiniPainterHub.Server.Entities
{
    public class Conversation
    {
        public int Id { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime UpdatedUtc { get; set; }

        public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();

        public ICollection<DirectMessage> Messages { get; set; } = new List<DirectMessage>();
    }
}
