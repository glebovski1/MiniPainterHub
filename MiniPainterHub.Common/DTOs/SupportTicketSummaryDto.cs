using System;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs;

public class SupportTicketSummaryDto
{
    public int Id { get; set; }

    [Required]
    public string Category { get; set; } = default!;

    [Required]
    [StringLength(SupportTicketRules.MaxSubjectLength)]
    public string Subject { get; set; } = default!;

    [Required]
    public string Status { get; set; } = default!;

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? LastStaffReplyUtc { get; set; }
    public DateTime? RequesterReadUtc { get; set; }
    public DateTime? ResolvedUtc { get; set; }
    public bool HasUnreadStaffReply { get; set; }

    [Required]
    [StringLength(120)]
    public string LatestMessagePreview { get; set; } = default!;

    [Required]
    public string RequesterUserId { get; set; } = default!;

    [Required]
    public string RequesterUserName { get; set; } = default!;

    [Required]
    public string RequesterDisplayName { get; set; } = default!;

    [StringLength(2048)]
    public string? RequesterAvatarUrl { get; set; }
}
