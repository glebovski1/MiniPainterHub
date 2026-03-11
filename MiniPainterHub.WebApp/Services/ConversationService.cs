using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;
using System.Net.Http;

namespace MiniPainterHub.WebApp.Services
{
    public sealed class ConversationService : IConversationService, IAsyncDisposable
    {
        private readonly ApiClient _api;
        private readonly NavigationManager _navigation;
        private readonly IJSRuntime _jsRuntime;
        private readonly object _conversationLoadLock = new();
        private HubConnection? _hubConnection;
        private readonly HashSet<int> _joinedConversations = new();
        private IReadOnlyList<ConversationSummaryDto> _conversations = Array.Empty<ConversationSummaryDto>();
        private Task<IReadOnlyList<ConversationSummaryDto>>? _loadConversationsTask;

        public ConversationService(ApiClient api, NavigationManager navigation, IJSRuntime jsRuntime)
        {
            _api = api;
            _navigation = navigation;
            _jsRuntime = jsRuntime;
        }

        public event Action? OnChange;
        public event Action<DirectMessageDto>? MessageReceived;
        public event Action<ConversationReadDto>? ConversationRead;

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

        public async Task<PagedResult<DirectMessageDto>> GetMessagesAsync(int conversationId, int? beforeMessageId = null, int pageSize = 50)
        {
            var beforeQuery = beforeMessageId.HasValue ? $"&beforeMessageId={beforeMessageId.Value}" : string.Empty;
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/conversations/{conversationId}/messages?pageSize={pageSize}{beforeQuery}");
            var result = await _api.SendAsync<PagedResult<DirectMessageDto>>(request);
            return result ?? new PagedResult<DirectMessageDto>();
        }

        public async Task<DirectMessageDto> SendMessageAsync(int conversationId, CreateDirectMessageDto dto)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/conversations/{conversationId}/messages")
            {
                Content = System.Net.Http.Json.JsonContent.Create(dto)
            };

            var message = await _api.SendAsync<DirectMessageDto>(request)
                ?? throw new InvalidOperationException("API returned no data when sending the message.");

            await GetConversationsAsync(forceRefresh: true);
            return message;
        }

        public async Task MarkReadAsync(int conversationId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/conversations/{conversationId}/read");
            await _api.SendAsync(request);
            await GetConversationsAsync(forceRefresh: true);
        }

        public async Task EnsureRealtimeAsync()
        {
            if (_hubConnection != null)
            {
                if (_hubConnection.State == HubConnectionState.Disconnected)
                {
                    await _hubConnection.StartAsync();
                }

                return;
            }

            var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_navigation.ToAbsoluteUri("/hubs/chat"), options =>
                {
                    options.AccessTokenProvider = async () =>
                        await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");
                })
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<ConversationChangedDto>("ConversationChanged", payload =>
            {
                _ = HandleConversationChangedAsync(payload);
            });

            _hubConnection.On<DirectMessageDto>("MessageReceived", payload =>
            {
                MessageReceived?.Invoke(payload);
                _ = RefreshAsync();
            });

            _hubConnection.On<ConversationReadDto>("ConversationRead", payload =>
            {
                ConversationRead?.Invoke(payload);
                _ = RefreshAsync();
            });

            await _hubConnection.StartAsync();
        }

        public async Task JoinConversationAsync(int conversationId)
        {
            await EnsureRealtimeAsync();
            if (_hubConnection == null || _joinedConversations.Contains(conversationId))
            {
                return;
            }

            await _hubConnection.InvokeAsync("JoinConversation", conversationId);
            _joinedConversations.Add(conversationId);
        }

        public async Task LeaveConversationAsync(int conversationId)
        {
            if (_hubConnection == null || !_joinedConversations.Contains(conversationId))
            {
                return;
            }

            await _hubConnection.InvokeAsync("LeaveConversation", conversationId);
            _joinedConversations.Remove(conversationId);
        }

        private async Task HandleConversationChangedAsync(ConversationChangedDto payload)
        {
            await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            await GetConversationsAsync(forceRefresh: true);
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

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
            }
        }
    }
}
