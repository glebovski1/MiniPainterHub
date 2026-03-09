using System;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class ConversationSummaryDto
    {
        public int Id { get; set; }

        [Required]
        public UserListItemDto OtherUser { get; set; } = default!;

        [StringLength(120)]
        public string? LatestMessagePreview { get; set; }

        public string? LatestMessageSenderUserId { get; set; }

        public DateTime? LatestMessageSentUtc { get; set; }

        public int UnreadCount { get; set; }
    }
}
