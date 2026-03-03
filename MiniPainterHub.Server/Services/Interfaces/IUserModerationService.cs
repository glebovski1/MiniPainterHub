using System.Threading.Tasks;
using MiniPainterHub.Common.DTOs;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IUserModerationService
    {
        Task<UserRestrictionDto> RestrictAsync(string actorUserId, string userId, SetUserRestrictionDto dto);
        Task<UserRestrictionDto> LiftAsync(string actorUserId, string userId);
        Task<UserRestrictionDto> SuspendAsync(string actorUserId, string userId, SetSuspensionDto dto);
        Task<UserRestrictionDto> UnsuspendAsync(string actorUserId, string userId);
        Task<UserRestrictionDto> GetOrDefaultAsync(string userId);
    }
}
