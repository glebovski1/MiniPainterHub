using MiniPainterHub.Common.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface ICommentService
    {
        Task<CommentDto> CreateAsync(string userId, int postId, CreateCommentDto dto);
        Task<PagedResult<CommentDto>> GetByPostIdAsync(int postId, int page = 1, int pageSize = 10);
        Task<bool> UpdateAsync(int commentId, string userId, UpdateCommentDto dto);
        Task<bool> DeleteAsync(int commentId, string userId, bool isAdmin = false);

    }
}
