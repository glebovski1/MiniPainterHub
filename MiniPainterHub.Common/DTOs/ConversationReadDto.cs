using System;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class ConversationReadDto
    {
        public int ConversationId { get; set; }

        [Required]
        public string ReaderUserId { get; set; } = default!;

        public DateTime LastReadUtc { get; set; }
    }
}
