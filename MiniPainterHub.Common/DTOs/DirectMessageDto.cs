using System;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class DirectMessageDto
    {
        public int Id { get; set; }

        public int ConversationId { get; set; }

        [Required]
        public string SenderUserId { get; set; } = default!;

        [Required]
        [StringLength(100)]
        public string SenderDisplayName { get; set; } = default!;

        [StringLength(2048)]
        public string? SenderAvatarUrl { get; set; }

        [Required]
        [StringLength(2000)]
        public string Body { get; set; } = default!;

        public DateTime SentUtc { get; set; }

        public bool IsMine { get; set; }
    }
}
