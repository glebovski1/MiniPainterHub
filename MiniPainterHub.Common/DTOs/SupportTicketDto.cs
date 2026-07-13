using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs;

public sealed class SupportTicketDto : SupportTicketSummaryDto
{
    [Required]
    public IReadOnlyList<SupportTicketMessageDto> Messages { get; set; } = Array.Empty<SupportTicketMessageDto>();
}
