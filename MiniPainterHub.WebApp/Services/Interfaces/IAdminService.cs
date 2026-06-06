using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MiniPainterHub.WebApp.Services.Interfaces
{
    public interface IAdminService
    {
        Task<ApiResult<PagedResult<AdminInboxItemDto>?>> GetInboxAsync(AdminInboxQueryDto query);
        Task<ApiResult<AdminInboxDetailDto?>> GetInboxDetailAsync(string targetType, string targetId);
        Task<bool> ReviewInboxItemAsync(string targetType, string targetId, AdminInboxReviewRequestDto request);
        Task<ApiResult<IReadOnlyList<AdminSiteControlDto>?>> GetControlsAsync();
        Task<ApiResult<AdminSiteControlDto?>> UpdateControlAsync(string key, UpdateAdminSiteControlRequestDto request);
        Task<ApiResult<AdminDashboardStatsDto?>> GetDashboardAsync(int windowHours);
    }
}
