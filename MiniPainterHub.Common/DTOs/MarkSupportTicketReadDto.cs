using System;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs;

public sealed class MarkSupportTicketReadDto
{
    [Required]
    public DateTime? LastStaffReplyUtc { get; set; }
}
