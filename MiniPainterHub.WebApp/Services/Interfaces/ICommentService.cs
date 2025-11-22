using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;

namespace MiniPainterHub.WebApp.Services.Interfaces
{
    public interface ICommentService
    {
        /// <summary>
        /// Fetches a paged list of comments for a post.
        /// </summary>
        Task<ApiResult<PagedResult<CommentDto>>> GetByPostAsync(int postId, int page, int pageSize);

        /// <summary>
        /// Posts a new comment and returns the created CommentDto.
        /// </summary>
        Task<ApiResult<CommentDto?>> CreateAsync(int postId, CreateCommentDto dto);

        // (Optional) you could add:
        // Task<bool> UpdateAsync(int commentId, UpdateCommentDto dto);
        // Task DeleteAsync(int commentId);
    }
}
