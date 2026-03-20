using MiniPainterHub.Common.DTOs;
using System.Threading.Tasks;

namespace MiniPainterHub.WebApp.Services.Interfaces
{
    public interface IAuthorMarkService
    {
        Task<AuthorMarkDto> CreateAsync(int postId, int imageId, CreateAuthorMarkDto dto);
        Task<AuthorMarkDto> UpdateAsync(int markId, UpdateAuthorMarkDto dto);
        Task DeleteAsync(int markId);
    }
}
