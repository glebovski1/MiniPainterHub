using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;

namespace MiniPainterHub.WebApp.Services;

public sealed class SupportTicketService : ISupportTicketService
{
    private readonly ApiClient _api;

    public SupportTicketService(ApiClient api)
    {
        _api = api;
    }

    public event Action? UnreadCountChanged;

    public int UnreadCount { get; private set; }

    public Task<ApiResult<PagedResult<SupportTicketSummaryDto>?>> GetMineAsync(SupportTicketQueryDto query) =>
        _api.SendForResultAsync<PagedResult<SupportTicketSummaryDto>?>(
            new HttpRequestMessage(HttpMethod.Get, $"api/support/tickets{BuildQuery(query)}"));

    public Task<ApiResult<SupportTicketDto?>> GetAsync(int ticketId) =>
        _api.SendForResultAsync<SupportTicketDto?>(
            new HttpRequestMessage(HttpMethod.Get, $"api/support/tickets/{ticketId}"));

    public Task<ApiResult<SupportTicketDto?>> CreateAsync(CreateSupportTicketDto request) =>
        _api.SendForResultAsync<SupportTicketDto?>(new HttpRequestMessage(HttpMethod.Post, "api/support/tickets")
        {
            Content = ApiClient.CreateJsonContent(request)
        });

    public Task<ApiResult<SupportTicketDto?>> ReplyAsync(int ticketId, CreateSupportTicketMessageDto request) =>
        _api.SendForResultAsync<SupportTicketDto?>(new HttpRequestMessage(HttpMethod.Post, $"api/support/tickets/{ticketId}/messages")
        {
            Content = ApiClient.CreateJsonContent(request)
        });

    public async Task<bool> MarkReadAsync(int ticketId, DateTime? lastStaffReplyUtc)
    {
        var success = await _api.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"api/support/tickets/{ticketId}/read")
        {
            Content = ApiClient.CreateJsonContent(new MarkSupportTicketReadDto { LastStaffReplyUtc = lastStaffReplyUtc })
        });
        if (success)
        {
            await RefreshUnreadCountAsync();
        }

        return success;
    }

    public async Task<int> RefreshUnreadCountAsync()
    {
        var result = await _api.SendForResultAsync<SupportUnreadCountDto?>(
            new HttpRequestMessage(HttpMethod.Get, "api/support/tickets/unread-count"),
            new ApiRequestOptions { SuppressErrorNotifications = true });

        if (!result.Success || result.Value is null)
        {
            return UnreadCount;
        }

        if (UnreadCount != result.Value.Count)
        {
            UnreadCount = result.Value.Count;
            UnreadCountChanged?.Invoke();
        }

        return UnreadCount;
    }

    public Task<ApiResult<PagedResult<SupportTicketSummaryDto>?>> GetAdminQueueAsync(SupportTicketQueryDto query) =>
        _api.SendForResultAsync<PagedResult<SupportTicketSummaryDto>?>(
            new HttpRequestMessage(HttpMethod.Get, $"api/admin/support/tickets{BuildQuery(query)}"));

    public Task<ApiResult<SupportTicketDto?>> GetAdminTicketAsync(int ticketId) =>
        _api.SendForResultAsync<SupportTicketDto?>(
            new HttpRequestMessage(HttpMethod.Get, $"api/admin/support/tickets/{ticketId}"));

    public Task<ApiResult<SupportTicketDto?>> ReplyAsAdminAsync(int ticketId, CreateSupportTicketMessageDto request) =>
        _api.SendForResultAsync<SupportTicketDto?>(new HttpRequestMessage(HttpMethod.Post, $"api/admin/support/tickets/{ticketId}/messages")
        {
            Content = ApiClient.CreateJsonContent(request)
        });

    public Task<ApiResult<SupportTicketDto?>> UpdateStatusAsync(int ticketId, UpdateSupportTicketStatusDto request) =>
        _api.SendForResultAsync<SupportTicketDto?>(new HttpRequestMessage(HttpMethod.Put, $"api/admin/support/tickets/{ticketId}/status")
        {
            Content = ApiClient.CreateJsonContent(request)
        });

    private static string BuildQuery(SupportTicketQueryDto query)
    {
        var values = new Dictionary<string, string?>
        {
            ["pageNumber"] = query.PageNumber.ToString(),
            ["pageSize"] = query.PageSize.ToString(),
            ["status"] = query.Status,
            ["category"] = query.Category,
            ["search"] = query.Search
        };

        var parts = values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value!)}");

        return "?" + string.Join("&", parts);
    }
}
