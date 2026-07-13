using System;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs;

public sealed class SupportTicketMessageDto
{
    public int Id { get; set; }
    public int TicketId { get; set; }

    [Required]
    public string AuthorUserId { get; set; } = default!;

    [Required]
    public string AuthorUserName { get; set; } = default!;

    [Required]
    public string AuthorDisplayName { get; set; } = default!;

    [StringLength(2048)]
    public string? AuthorAvatarUrl { get; set; }

    [Required]
    [StringLength(SupportTicketRules.MaxMessageLength)]
    public string Body { get; set; } = default!;

    public DateTime SentUtc { get; set; }
    public bool IsStaffReply { get; set; }
    public bool IsMine { get; set; }
}
