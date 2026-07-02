using System.Net.Http;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;

namespace MiniPainterHub.WebApp.Services;

public class NewsAnnouncementService : INewsAnnouncementService
{
    private readonly ApiClient _api;

    public NewsAnnouncementService(ApiClient api)
    {
        _api = api;
    }

    public async Task<ApiResult<PagedResult<NewsAnnouncementSummaryDto>>> GetAllAsync(int page, int pageSize)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/news?page={page}&pageSize={pageSize}");
        var result = await _api.SendForResultAsync<PagedResult<NewsAnnouncementSummaryDto>>(request);
        return result with
        {
            Value = result.Value ?? new PagedResult<NewsAnnouncementSummaryDto>()
        };
    }

    public async Task<NewsAnnouncementDto> GetByIdAsync(int announcementId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/news/{announcementId}");
        var dto = await _api.SendAsync<NewsAnnouncementDto>(request);
        if (dto is null)
        {
            throw new InvalidOperationException($"No announcement returned from API for ID {announcementId}.");
        }

        return dto;
    }

    public async Task<NewsAnnouncementDto> CreateAsync(CreateNewsAnnouncementDto dto)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/news")
        {
            Content = ApiClient.CreateJsonContent(dto)
        };

        var result = await _api.SendAsync<NewsAnnouncementDto>(request);
        if (result is null)
        {
            throw new InvalidOperationException("API returned no data when creating announcement.");
        }

        return result;
    }
}
