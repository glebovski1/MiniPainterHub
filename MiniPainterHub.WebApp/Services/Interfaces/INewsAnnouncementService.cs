using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;

namespace MiniPainterHub.WebApp.Services.Interfaces;

public interface INewsAnnouncementService
{
    Task<ApiResult<PagedResult<NewsAnnouncementSummaryDto>>> GetAllAsync(int page, int pageSize);

    Task<NewsAnnouncementDto> GetByIdAsync(int announcementId);

    Task<NewsAnnouncementDto> CreateAsync(CreateNewsAnnouncementDto dto);
}
