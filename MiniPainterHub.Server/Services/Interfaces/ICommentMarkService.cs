using MiniPainterHub.Common.DTOs;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface ICommentMarkService
    {
        Task<CommentMarkDto> GetByCommentIdAsync(int commentId, bool includeDeleted = false);
        Task<CommentMarkDto> UpsertAsync(int commentId, string userId, ViewerMarkDraftDto dto);
        Task DeleteAsync(int commentId, string userId);
    }
}
