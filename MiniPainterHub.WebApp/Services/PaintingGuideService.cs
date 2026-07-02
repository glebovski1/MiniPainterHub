using System.Net.Http;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;

namespace MiniPainterHub.WebApp.Services;

public class PaintingGuideService : IPaintingGuideService
{
    private readonly ApiClient _api;

    public PaintingGuideService(ApiClient api)
    {
        _api = api;
    }

    public async Task<ApiResult<PagedResult<PaintingGuideSummaryDto>>> GetAllAsync(int page, int pageSize)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/guides?page={page}&pageSize={pageSize}");
        var result = await _api.SendForResultAsync<PagedResult<PaintingGuideSummaryDto>>(request);
        return result with
        {
            Value = result.Value ?? new PagedResult<PaintingGuideSummaryDto>()
        };
    }

    public async Task<PaintingGuideDto> GetByIdAsync(int guideId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/guides/{guideId}");
        var dto = await _api.SendAsync<PaintingGuideDto>(request);
        if (dto is null)
        {
            throw new InvalidOperationException($"No guide returned from API for ID {guideId}.");
        }

        return dto;
    }

    public async Task<PaintingGuideDto> CreateAsync(CreatePaintingGuideDto dto)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/guides")
        {
            Content = ApiClient.CreateJsonContent(dto)
        };

        var result = await _api.SendAsync<PaintingGuideDto>(request);
        if (result is null)
        {
            throw new InvalidOperationException("API returned no data when creating guide.");
        }

        return result;
    }

    public async Task<PaintingGuideDto> CreateWithStepPhotosAsync(MultipartFormDataContent content)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/guides/with-step-photos")
        {
            Content = content
        };

        var result = await _api.SendAsync<PaintingGuideDto>(request);
        if (result is null)
        {
            throw new InvalidOperationException("API returned no data when creating guide with step photos.");
        }

        return result;
    }
}
