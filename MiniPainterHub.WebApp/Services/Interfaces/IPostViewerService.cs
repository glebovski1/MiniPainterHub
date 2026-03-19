using MiniPainterHub.Common.DTOs;
using System.Threading.Tasks;

namespace MiniPainterHub.WebApp.Services.Interfaces
{
    public interface IPostViewerService
    {
        Task<PostViewerDto> GetAsync(int postId);
    }
}
