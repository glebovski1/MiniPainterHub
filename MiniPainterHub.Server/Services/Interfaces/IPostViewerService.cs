using MiniPainterHub.Common.DTOs;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IPostViewerService
    {
        Task<PostViewerDto> GetAsync(int postId, string? currentUserId);
    }
}
