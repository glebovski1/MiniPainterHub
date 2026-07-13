using MiniPainterHub.Common.DTOs;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces;

public interface ISupportTicketService
{
    Task<PagedResult<SupportTicketSummaryDto>> GetForUserAsync(string userId, SupportTicketQueryDto query);
    Task<PagedResult<SupportTicketSummaryDto>> GetForAdminAsync(SupportTicketQueryDto query);
    Task<SupportTicketDto> GetForUserAsync(string userId, int ticketId);
    Task<SupportTicketDto> GetForAdminAsync(string currentUserId, int ticketId);
    Task<SupportTicketDto> CreateAsync(string userId, CreateSupportTicketDto request);
    Task<SupportTicketDto> ReplyAsUserAsync(string userId, int ticketId, CreateSupportTicketMessageDto request);
    Task<SupportTicketDto> ReplyAsAdminAsync(string adminUserId, int ticketId, CreateSupportTicketMessageDto request);
    Task<SupportTicketDto> UpdateStatusAsAdminAsync(string adminUserId, int ticketId, UpdateSupportTicketStatusDto request);
    Task MarkReadAsync(string userId, int ticketId, MarkSupportTicketReadDto request);
    Task<SupportUnreadCountDto> GetUnreadCountAsync(string userId);
}
