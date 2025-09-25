using MiniPainterHub.Common.DTOs;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IProfileService
    {
        Task<UserProfileDto> CreateAsync(string userId, CreateUserProfileDto dto);
        Task<UserProfileDto> UpdateAsync(string userId, UpdateUserProfileDto dto);
        Task<UserProfileDto> GetByUserIdAsync(string userId);
        Task<UserProfileDto> SetAvatarUrlAsync(string userId, string? avatarUrl);



        Task<UserProfileDto> GetUserProfileById(string id);



    }
}
