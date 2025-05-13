using MiniPainterHub.Common.DTOs;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IPostService
    {
        Task<PostDto> CreateAsync(string userId, CreatePostDto dto);
        Task<PagedResult<PostDto>> GetAllAsync(int page, int pageSize);
        Task<PostDto> GetByIdAsync(int postId);
        Task<bool> UpdateAsync(int postId, string userId, UpdatePostDto dto);
        Task<bool> DeleteAsync(int postId, string userId);
    }
}
