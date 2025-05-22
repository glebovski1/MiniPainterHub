using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface ILikeService
    {
        Task<int> GetCountAsync(int postId);
        Task<bool> IsLikedAsync(int postId, string userId);
        Task<bool> ToggleAsync(int postId, string userId);
        Task<bool> RemoveAsync(int postId, string userId);
    }
}
