using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;

namespace MiniPainterHub.WebApp.Services.Interfaces;

public interface IPaintingGuideService
{
    Task<ApiResult<PagedResult<PaintingGuideSummaryDto>>> GetAllAsync(int page, int pageSize);

    Task<PaintingGuideDto> GetByIdAsync(int guideId);

    Task<PaintingGuideDto> CreateAsync(CreatePaintingGuideDto dto);

    Task<PaintingGuideDto> CreateWithStepPhotosAsync(MultipartFormDataContent content);
}
