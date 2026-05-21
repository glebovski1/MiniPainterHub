using MiniPainterHub.Common.DTOs;

namespace MiniPainterHub.WebApp.Services.Interfaces;

public interface IConversationSummaryService
{
    event Action? OnChange;

    int UnreadConversationCount { get; }

    Task<IReadOnlyList<ConversationSummaryDto>> GetConversationsAsync(bool forceRefresh = false);

    Task<ConversationSummaryDto> OpenDirectConversationAsync(string userId);
}
