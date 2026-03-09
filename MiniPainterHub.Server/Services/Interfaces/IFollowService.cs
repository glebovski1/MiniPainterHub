using MiniPainterHub.Common.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IFollowService
    {
        Task FollowAsync(string followerUserId, string followedUserId);
        Task UnfollowAsync(string followerUserId, string followedUserId);
        Task<IReadOnlyList<UserListItemDto>> GetFollowersAsync(string userId);
        Task<IReadOnlyList<UserListItemDto>> GetFollowingAsync(string userId);
    }
}
