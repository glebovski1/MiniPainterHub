using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs;

public sealed class CreateSupportTicketDto
{
    [Required]
    public string Category { get; set; } = default!;

    [Required]
    [StringLength(SupportTicketRules.MaxSubjectLength)]
    public string Subject { get; set; } = default!;

    [Required]
    [StringLength(SupportTicketRules.MaxMessageLength)]
    public string Message { get; set; } = default!;
}
