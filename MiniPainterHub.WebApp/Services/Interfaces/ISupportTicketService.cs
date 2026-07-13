using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;

namespace MiniPainterHub.WebApp.Services.Interfaces;

public interface ISupportTicketService
{
    event Action? UnreadCountChanged;

    int UnreadCount { get; }

    Task<ApiResult<PagedResult<SupportTicketSummaryDto>?>> GetMineAsync(SupportTicketQueryDto query);
    Task<ApiResult<SupportTicketDto?>> GetAsync(int ticketId);
    Task<ApiResult<SupportTicketDto?>> CreateAsync(CreateSupportTicketDto request);
    Task<ApiResult<SupportTicketDto?>> ReplyAsync(int ticketId, CreateSupportTicketMessageDto request);
    Task<bool> MarkReadAsync(int ticketId, DateTime? lastStaffReplyUtc);
    Task<int> RefreshUnreadCountAsync();
    Task<ApiResult<PagedResult<SupportTicketSummaryDto>?>> GetAdminQueueAsync(SupportTicketQueryDto query);
    Task<ApiResult<SupportTicketDto?>> GetAdminTicketAsync(int ticketId);
    Task<ApiResult<SupportTicketDto?>> ReplyAsAdminAsync(int ticketId, CreateSupportTicketMessageDto request);
    Task<ApiResult<SupportTicketDto?>> UpdateStatusAsync(int ticketId, UpdateSupportTicketStatusDto request);
}
