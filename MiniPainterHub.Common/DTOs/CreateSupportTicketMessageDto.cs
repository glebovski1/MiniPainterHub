using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs;

public sealed class CreateSupportTicketMessageDto
{
    [Required]
    [StringLength(SupportTicketRules.MaxMessageLength)]
    public string Body { get; set; } = default!;
}
