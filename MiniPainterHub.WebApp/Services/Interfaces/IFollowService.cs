using MiniPainterHub.Common.DTOs;

namespace MiniPainterHub.WebApp.Services.Interfaces
{
    public interface IFollowService
    {
        Task FollowAsync(string userId);
        Task UnfollowAsync(string userId);
        Task<IReadOnlyList<UserListItemDto>> GetFollowersAsync();
        Task<IReadOnlyList<UserListItemDto>> GetFollowingAsync();
    }
}
