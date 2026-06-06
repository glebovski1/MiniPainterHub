using MiniPainterHub.Common.DTOs;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IAdminInboxService
    {
        Task<PagedResult<AdminInboxItemDto>> GetInboxAsync(AdminInboxQueryDto query);
        Task<AdminInboxDetailDto> GetDetailAsync(string targetType, string targetId);
        Task ReviewAsync(string targetType, string targetId, string actorUserId, AdminInboxReviewRequestDto request);
    }
}
