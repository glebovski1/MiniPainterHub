using MiniPainterHub.Common.DTOs;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IProfileService
    {
        Task<ProfileDto> CreateAsync(string userId, CreateProfileDto dto);
        Task<ProfileDto> UpdateAsync(string userId, UpdateProfileDto dto);
        Task<ProfileDto> GetByUserIdAsync(string userId);
    }
}
