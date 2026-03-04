using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public class ModerationService : IModerationService
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ModerationService(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task ModeratePostAsync(int postId, string actorUserId, bool hide, string? reason)
        {
            var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId) ?? throw new NotFoundException("Post not found.");
            post.IsDeleted = hide;
            post.ModeratedUtc = DateTime.UtcNow;
            post.ModeratedByUserId = actorUserId;
            post.ModerationReason = reason;
            post.UpdatedUtc = DateTime.UtcNow;
            post.SoftDeletedUtc = hide ? DateTime.UtcNow : null;

            await AddAuditAsync(actorUserId, hide ? "PostHide" : "PostRestore", "Post", postId.ToString(), reason);
            await _db.SaveChangesAsync();
        }

        public async Task ModerateCommentAsync(int commentId, string actorUserId, bool hide, string? reason)
        {
            var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == commentId) ?? throw new NotFoundException("Comment not found.");
            comment.IsDeleted = hide;
            comment.ModeratedUtc = DateTime.UtcNow;
            comment.ModeratedByUserId = actorUserId;
            comment.ModerationReason = reason;
            comment.UpdatedUtc = DateTime.UtcNow;
            comment.SoftDeletedUtc = hide ? DateTime.UtcNow : null;

            await AddAuditAsync(actorUserId, hide ? "CommentHide" : "CommentRestore", "Comment", commentId.ToString(), reason);
            await _db.SaveChangesAsync();
        }

        public async Task SuspendUserAsync(string targetUserId, string actorUserId, DateTime? suspendedUntilUtc, string? reason)
        {
            var target = await _userManager.FindByIdAsync(targetUserId) ?? throw new NotFoundException("User not found.");
            target.SuspendedUntilUtc = suspendedUntilUtc;
            target.SuspensionReason = reason;
            target.SuspensionUpdatedUtc = DateTime.UtcNow;
            await _userManager.UpdateAsync(target);

            await AddAuditAsync(actorUserId, "UserSuspend", "User", targetUserId, reason);
            await _db.SaveChangesAsync();
        }

        public async Task UnsuspendUserAsync(string targetUserId, string actorUserId, string? reason)
        {
            var target = await _userManager.FindByIdAsync(targetUserId) ?? throw new NotFoundException("User not found.");
            target.SuspendedUntilUtc = null;
            target.SuspensionReason = reason;
            target.SuspensionUpdatedUtc = DateTime.UtcNow;
            await _userManager.UpdateAsync(target);

            await AddAuditAsync(actorUserId, "UserUnsuspend", "User", targetUserId, reason);
            await _db.SaveChangesAsync();
        }

        public async Task<PagedResult<ModerationAuditDto>> GetAuditAsync(ModerationAuditQueryDto query)
        {
            var dbq = _db.ModerationAuditLogs.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(query.TargetType)) dbq = dbq.Where(x => x.TargetType == query.TargetType);
            if (!string.IsNullOrWhiteSpace(query.ActorUserId)) dbq = dbq.Where(x => x.ActorUserId == query.ActorUserId);
            if (!string.IsNullOrWhiteSpace(query.ActionType)) dbq = dbq.Where(x => x.ActionType == query.ActionType);

            var total = await dbq.CountAsync();
            var items = await dbq.OrderByDescending(x => x.CreatedUtc)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(x => new ModerationAuditDto
                {
                    Id = x.Id,
                    CreatedUtc = x.CreatedUtc,
                    ActorUserId = x.ActorUserId,
                    ActorRole = x.ActorRole,
                    ActionType = x.ActionType,
                    TargetType = x.TargetType,
                    TargetId = x.TargetId,
                    Reason = x.Reason,
                    MetadataJson = x.MetadataJson
                }).ToListAsync();

            return new PagedResult<ModerationAuditDto>{ Items=items, TotalCount=total, PageNumber=query.Page, PageSize=query.PageSize };
        }

        private async Task AddAuditAsync(string actorUserId, string actionType, string targetType, string targetId, string? reason)
        {
            var actor = await _userManager.FindByIdAsync(actorUserId) ?? throw new UnauthorizedAccessException("Actor not found.");
            var roles = await _userManager.GetRolesAsync(actor);
            var role = roles.FirstOrDefault() ?? "Unknown";
            _db.ModerationAuditLogs.Add(new ModerationAuditLog
            {
                CreatedUtc = DateTime.UtcNow,
                ActorUserId = actorUserId,
                ActorRole = role,
                ActionType = actionType,
                TargetType = targetType,
                TargetId = targetId,
                Reason = reason
            });
        }
    }
}
