using MiniPainterHub.Common.DTOs;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface ILikeService
    {
        Task<LikeDto> GetLikesAsync(int postId, string? userId);
        Task<bool> IsLikedAsync(int postId, string userId);
        Task<bool> ToggleAsync(int postId, string userId);
        Task<bool> RemoveAsync(int postId, string userId);
    }
}
