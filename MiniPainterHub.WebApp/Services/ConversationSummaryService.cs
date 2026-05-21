using System.Net.Http;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;

namespace MiniPainterHub.WebApp.Services;

public sealed class ConversationSummaryService : IConversationSummaryService
{
    private readonly ApiClient _api;
    private readonly object _conversationLoadLock = new();
    private IReadOnlyList<ConversationSummaryDto> _conversations = Array.Empty<ConversationSummaryDto>();
    private Task<IReadOnlyList<ConversationSummaryDto>>? _loadConversationsTask;

    public ConversationSummaryService(ApiClient api)
    {
        _api = api;
    }

    public event Action? OnChange;

    public int UnreadConversationCount => _conversations.Count(c => c.UnreadCount > 0);

    public Task<IReadOnlyList<ConversationSummaryDto>> GetConversationsAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _conversations.Count > 0)
        {
            return Task.FromResult(_conversations);
        }

        Task<IReadOnlyList<ConversationSummaryDto>> loadTask;
        lock (_conversationLoadLock)
        {
            if (!forceRefresh && _conversations.Count > 0)
            {
                return Task.FromResult(_conversations);
            }

            _loadConversationsTask ??= LoadConversationsAsync();
            loadTask = _loadConversationsTask;
        }

        return AwaitConversationLoadAsync(loadTask);
    }

    public async Task<ConversationSummaryDto> OpenDirectConversationAsync(string userId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/conversations/direct/{Uri.EscapeDataString(userId)}");
        var result = await _api.SendAsync<ConversationSummaryDto>(request)
            ?? throw new InvalidOperationException("API returned no data when creating the conversation.");

        await GetConversationsAsync(forceRefresh: true);
        return result;
    }

    private async Task<IReadOnlyList<ConversationSummaryDto>> LoadConversationsAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/conversations");
        var result = await _api.SendAsync<List<ConversationSummaryDto>>(request);
        _conversations = (result ?? new List<ConversationSummaryDto>())
            .OrderByDescending(c => c.LatestMessageSentUtc ?? DateTime.MinValue)
            .ToList();
        OnChange?.Invoke();
        return _conversations;
    }

    private async Task<IReadOnlyList<ConversationSummaryDto>> AwaitConversationLoadAsync(Task<IReadOnlyList<ConversationSummaryDto>> loadTask)
    {
        try
        {
            return await loadTask;
        }
        finally
        {
            lock (_conversationLoadLock)
            {
                if (ReferenceEquals(_loadConversationsTask, loadTask))
                {
                    _loadConversationsTask = null;
                }
            }
        }
    }
}
