using MiniPainterHub.Common.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IConversationService
    {
        Task<IReadOnlyList<ConversationSummaryDto>> GetConversationsAsync(string userId);
        Task<ConversationSummaryDto> GetOrCreateDirectConversationAsync(string userId, string otherUserId);
        Task<PagedResult<DirectMessageDto>> GetMessagesAsync(string userId, int conversationId, int? beforeMessageId, int pageSize);
        Task<DirectMessageDto> SendMessageAsync(string userId, int conversationId, CreateDirectMessageDto dto);
        Task MarkReadAsync(string userId, int conversationId);
        Task<bool> IsParticipantAsync(string userId, int conversationId);
    }
}
