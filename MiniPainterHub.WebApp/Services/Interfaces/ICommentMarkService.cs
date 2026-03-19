using MiniPainterHub.Common.DTOs;
using System.Threading.Tasks;

namespace MiniPainterHub.WebApp.Services.Interfaces
{
    public interface ICommentMarkService
    {
        Task<CommentMarkDto> GetByCommentIdAsync(int commentId, bool includeDeleted = false);
        Task<CommentMarkDto> UpsertAsync(int commentId, ViewerMarkDraftDto dto);
        Task DeleteAsync(int commentId);
    }
}
