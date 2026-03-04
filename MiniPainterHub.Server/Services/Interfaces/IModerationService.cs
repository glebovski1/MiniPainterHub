using MiniPainterHub.Common.DTOs;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IModerationService
    {
        Task ModeratePostAsync(int postId, string actorUserId, bool hide, string? reason);
        Task ModerateCommentAsync(int commentId, string actorUserId, bool hide, string? reason);
        Task SuspendUserAsync(string targetUserId, string actorUserId, DateTime? suspendedUntilUtc, string? reason);
        Task UnsuspendUserAsync(string targetUserId, string actorUserId, string? reason);
        Task<PagedResult<ModerationAuditDto>> GetAuditAsync(ModerationAuditQueryDto query);
        Task<IReadOnlyList<ModerationUserLookupDto>> SearchUsersAsync(string? query, int limit);
        Task<ModerationPostPreviewDto> GetPostPreviewAsync(int postId);
        Task<ModerationCommentPreviewDto> GetCommentPreviewAsync(int commentId);
    }
}
