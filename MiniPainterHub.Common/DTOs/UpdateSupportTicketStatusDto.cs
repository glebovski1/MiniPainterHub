using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs;

public sealed class UpdateSupportTicketStatusDto
{
    [Required]
    public string Status { get; set; } = default!;
}
