using Microsoft.AspNetCore.Components.Forms;
using MiniPainterHub.Common.DTOs;

namespace MiniPainterHub.WebApp.Services.Interfaces
{
    public interface IProfileService
    {
        Task<UserProfileDto?> GetMineAsync();
        Task<UserProfileDto> CreateMineAsync(CreateUserProfileDto dto);
        Task<UserProfileDto> UpdateMineAsync(UpdateUserProfileDto dto);
        Task<UserProfileDto> UploadAvatarAsync(IBrowserFile file, long maxSizeBytes = 5_000_000);
        Task<UserProfileDto> RemoveAvatarAsync();

        Task<UserProfileDto> GetUserProfileById(string id);
    }
}
