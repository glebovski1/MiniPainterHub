using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MiniPainterHub.WebApp.Services.Interfaces
{
    public interface IModerationService
    {
        Task<bool> HidePostAsync(int postId, ModerationActionRequestDto request);
        Task<bool> RestorePostAsync(int postId, ModerationActionRequestDto request);
        Task<bool> HideCommentAsync(int commentId, ModerationActionRequestDto request);
        Task<bool> RestoreCommentAsync(int commentId, ModerationActionRequestDto request);
        Task<bool> SuspendUserAsync(string userId, SuspendUserRequestDto request);
        Task<bool> UnsuspendUserAsync(string userId, ModerationActionRequestDto request);
        Task<ApiResult<PagedResult<ModerationAuditDto>?>> GetAuditAsync(ModerationAuditQueryDto query);
        Task<ApiResult<IReadOnlyList<ModerationUserLookupDto>?>> SearchUsersAsync(string? query, int limit = 10);
        Task<ApiResult<ModerationPostPreviewDto?>> GetPostPreviewAsync(int postId);
        Task<ApiResult<ModerationCommentPreviewDto?>> GetCommentPreviewAsync(int commentId);
    }
}
