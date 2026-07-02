using MiniPainterHub.Common.DTOs;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces;

public interface INewsAnnouncementService
{
    Task<PagedResult<NewsAnnouncementSummaryDto>> GetAllAsync(int page, int pageSize);

    Task<NewsAnnouncementDto> GetByIdAsync(int announcementId);

    Task<NewsAnnouncementDto> CreateAsync(string userId, CreateNewsAnnouncementDto dto);
}
