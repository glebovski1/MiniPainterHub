using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Identity;
using System;
using System.Collections.Generic;

namespace MiniPainterHub.Server.Entities;

public sealed class SupportTicket
{
    public int Id { get; set; }
    public string RequesterUserId { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string Subject { get; set; } = default!;
    public string Status { get; set; } = SupportTicketStatuses.New;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? LastStaffReplyUtc { get; set; }
    public DateTime? RequesterReadUtc { get; set; }
    public DateTime? ResolvedUtc { get; set; }

    public ApplicationUser RequesterUser { get; set; } = default!;
    public ICollection<SupportTicketMessage> Messages { get; set; } = new List<SupportTicketMessage>();
}
