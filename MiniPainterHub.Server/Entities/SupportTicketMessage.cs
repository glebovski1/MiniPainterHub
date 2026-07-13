using MiniPainterHub.Server.Identity;
using System;

namespace MiniPainterHub.Server.Entities;

public sealed class SupportTicketMessage
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string AuthorUserId { get; set; } = default!;
    public string Body { get; set; } = default!;
    public DateTime SentUtc { get; set; }
    public bool IsStaffReply { get; set; }

    public SupportTicket Ticket { get; set; } = default!;
    public ApplicationUser AuthorUser { get; set; } = default!;
}
