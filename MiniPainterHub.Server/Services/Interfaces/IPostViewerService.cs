using MiniPainterHub.Common.DTOs;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IPostViewerService
    {
        Task<PostViewerDto> GetAsync(int postId, string? currentUserId);
        Task<PostExperienceDto> GetExperienceAsync(int postId, string? currentUserId);
    }
}
