using MiniPainterHub.Common.DTOs;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IAuthorMarkService
    {
        Task<AuthorMarkDto> CreateAsync(int postId, int postImageId, string userId, CreateAuthorMarkDto dto);
        Task<AuthorMarkDto> UpdateAsync(int markId, string userId, UpdateAuthorMarkDto dto);
        Task DeleteAsync(int markId, string userId);
    }
}
