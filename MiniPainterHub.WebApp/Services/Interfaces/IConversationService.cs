using MiniPainterHub.Common.DTOs;

namespace MiniPainterHub.WebApp.Services.Interfaces
{
    public interface IConversationService
    {
        event Action? OnChange;
        event Action<DirectMessageDto>? MessageReceived;
        event Action<ConversationReadDto>? ConversationRead;

        int UnreadConversationCount { get; }

        Task<IReadOnlyList<ConversationSummaryDto>> GetConversationsAsync(bool forceRefresh = false);
        Task<ConversationSummaryDto> OpenDirectConversationAsync(string userId);
        Task<PagedResult<DirectMessageDto>> GetMessagesAsync(int conversationId, int? beforeMessageId = null, int pageSize = 50);
        Task<DirectMessageDto> SendMessageAsync(int conversationId, CreateDirectMessageDto dto);
        Task MarkReadAsync(int conversationId);
        Task EnsureRealtimeAsync();
        Task JoinConversationAsync(int conversationId);
        Task LeaveConversationAsync(int conversationId);
    }
}
