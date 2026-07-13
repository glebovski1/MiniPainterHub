namespace MiniPainterHub.Common.DTOs;

public sealed class SupportTicketQueryDto
{
    public string? Status { get; set; }
    public string? Category { get; set; }
    public string? Search { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
