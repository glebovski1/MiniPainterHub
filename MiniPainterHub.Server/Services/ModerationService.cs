using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public class ModerationService : IModerationService
    {
        private static readonly string[] ActorRolePriority = { "Admin", "Moderator" };
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

            if (!hide && !CanRestoreModeratedContent(post.IsDeleted, post.ModeratedByUserId, post.ModeratedUtc, post.SoftDeletedUtc))
            {
                throw new DomainValidationException(
                    "Only posts hidden by moderation can be restored.",
                    new System.Collections.Generic.Dictionary<string, string[]>
                    {
                        ["postId"] = new[] { "The post appears to be deleted outside moderation and cannot be restored here." }
                    });
            }

            var now = DateTime.UtcNow;
            post.IsDeleted = hide;
            post.ModeratedUtc = now;
            post.ModeratedByUserId = actorUserId;
            post.ModerationReason = reason;
            post.UpdatedUtc = now;
            post.SoftDeletedUtc = hide ? now : null;

            await AddAuditAsync(actorUserId, hide ? "PostHide" : "PostRestore", "Post", postId.ToString(), reason);
            await _db.SaveChangesAsync();
        }

        public async Task ModerateCommentAsync(int commentId, string actorUserId, bool hide, string? reason)
        {
            var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == commentId) ?? throw new NotFoundException("Comment not found.");

            if (!hide && !CanRestoreModeratedContent(comment.IsDeleted, comment.ModeratedByUserId, comment.ModeratedUtc, comment.SoftDeletedUtc))
            {
                throw new DomainValidationException(
                    "Only comments hidden by moderation can be restored.",
                    new System.Collections.Generic.Dictionary<string, string[]>
                    {
                        ["commentId"] = new[] { "The comment appears to be deleted outside moderation and cannot be restored here." }
                    });
            }

            var now = DateTime.UtcNow;
            comment.IsDeleted = hide;
            comment.ModeratedUtc = now;
            comment.ModeratedByUserId = actorUserId;
            comment.ModerationReason = reason;
            comment.UpdatedUtc = now;
            comment.SoftDeletedUtc = hide ? now : null;

            await AddAuditAsync(actorUserId, hide ? "CommentHide" : "CommentRestore", "Comment", commentId.ToString(), reason);
            await _db.SaveChangesAsync();
        }

        public async Task SuspendUserAsync(string targetUserId, string actorUserId, DateTime? suspendedUntilUtc, string? reason)
        {
            if (!suspendedUntilUtc.HasValue || suspendedUntilUtc.Value <= DateTime.UtcNow)
            {
                throw new DomainValidationException(
                    "Suspension expiry must be in the future.",
                    new System.Collections.Generic.Dictionary<string, string[]>
                    {
                        ["suspendedUntilUtc"] = new[] { "Provide a UTC timestamp in the future to suspend a user." }
                    });
            }

            var target = await _userManager.FindByIdAsync(targetUserId) ?? throw new NotFoundException("User not found.");
            target.SuspendedUntilUtc = suspendedUntilUtc;
            target.SuspensionReason = reason;
            target.SuspensionUpdatedUtc = DateTime.UtcNow;
            await _userManager.UpdateAsync(target);

            await AddAuditAsync(actorUserId, "UserSuspend", "User", targetUserId, reason);
            await _db.SaveChangesAsync();
        }

        private static bool CanRestoreModeratedContent(
            bool isDeleted,
            string? moderatedByUserId,
            DateTime? moderatedUtc,
            DateTime? softDeletedUtc)
        {
            if (!isDeleted)
            {
                return false;
            }

            // Backward compatibility for rows that were marked deleted before SoftDeletedUtc was introduced.
            if (!softDeletedUtc.HasValue)
            {
                return true;
            }

            // User-driven delete flows set SoftDeletedUtc but do not set moderation metadata.
            if (string.IsNullOrWhiteSpace(moderatedByUserId) || !moderatedUtc.HasValue)
            {
                return false;
            }

            // Restore is safe only when moderation is the latest delete-related action.
            return moderatedUtc.Value >= softDeletedUtc.Value;
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
            var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            if (query.Page <= 0)
            {
                errors["page"] = new[] { "Page number must be at least 1." };
            }

            if (query.PageSize <= 0)
            {
                errors["pageSize"] = new[] { "Page size must be greater than 0." };
            }

            if (errors.Count > 0)
            {
                throw new DomainValidationException("Audit query is invalid.", errors);
            }

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

            return new PagedResult<ModerationAuditDto> { Items = items, TotalCount = total, PageNumber = query.Page, PageSize = query.PageSize };
        }

        public async Task<IReadOnlyList<ModerationUserLookupDto>> SearchUsersAsync(string? query, int limit)
        {
            var safeLimit = limit <= 0 ? 10 : Math.Min(limit, 50);
            var now = DateTime.UtcNow;
            IQueryable<ApplicationUser> usersQuery = _db.Users.AsNoTracking();

            if (string.IsNullOrWhiteSpace(query))
            {
                usersQuery = usersQuery
                    .Where(u => u.SuspendedUntilUtc.HasValue && u.SuspendedUntilUtc.Value > now)
                    .OrderByDescending(u => u.SuspendedUntilUtc);
            }
            else
            {
                var term = query.Trim();
                usersQuery = usersQuery
                    .Where(u =>
                        (u.UserName != null && u.UserName.Contains(term)) ||
                        (u.Email != null && u.Email.Contains(term)) ||
                        u.Id.Contains(term))
                    .OrderBy(u => u.UserName);
            }

            var users = await usersQuery
                .Take(safeLimit)
                .ToListAsync();

            var results = new List<ModerationUserLookupDto>(users.Count);
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                results.Add(new ModerationUserLookupDto
                {
                    UserId = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    IsSuspended = user.SuspendedUntilUtc.HasValue && user.SuspendedUntilUtc.Value > now,
                    SuspendedUntilUtc = user.SuspendedUntilUtc,
                    SuspensionReason = user.SuspensionReason,
                    Roles = roles.ToArray()
                });
            }

            return results;
        }

        public async Task<ModerationPostPreviewDto> GetPostPreviewAsync(int postId)
        {
            var post = await _db.Posts
                .AsNoTracking()
                .Where(p => p.Id == postId)
                .Select(p => new ModerationPostPreviewDto
                {
                    PostId = p.Id,
                    Title = p.Title,
                    ContentSnippet = p.Content.Length > 240 ? p.Content.Substring(0, 240) + "..." : p.Content,
                    CreatedByUserId = p.CreatedById,
                    IsDeleted = p.IsDeleted,
                    CreatedUtc = p.CreatedUtc,
                    UpdatedUtc = p.UpdatedUtc,
                    ModeratedByUserId = p.ModeratedByUserId,
                    ModeratedUtc = p.ModeratedUtc,
                    ModerationReason = p.ModerationReason
                })
                .FirstOrDefaultAsync();

            return post ?? throw new NotFoundException("Post not found.");
        }

        public async Task<ModerationCommentPreviewDto> GetCommentPreviewAsync(int commentId)
        {
            var comment = await _db.Comments
                .AsNoTracking()
                .Where(c => c.Id == commentId)
                .Select(c => new ModerationCommentPreviewDto
                {
                    CommentId = c.Id,
                    PostId = c.PostId,
                    AuthorUserId = c.AuthorId,
                    TextSnippet = c.Text.Length > 240 ? c.Text.Substring(0, 240) + "..." : c.Text,
                    IsDeleted = c.IsDeleted,
                    CreatedUtc = c.CreatedUtc,
                    UpdatedUtc = c.UpdatedUtc,
                    ModeratedByUserId = c.ModeratedByUserId,
                    ModeratedUtc = c.ModeratedUtc,
                    ModerationReason = c.ModerationReason
                })
                .FirstOrDefaultAsync();

            return comment ?? throw new NotFoundException("Comment not found.");
        }

        private async Task AddAuditAsync(string actorUserId, string actionType, string targetType, string targetId, string? reason)
        {
            var actor = await _userManager.FindByIdAsync(actorUserId) ?? throw new UnauthorizedAccessException("Actor not found.");
            var roles = await _userManager.GetRolesAsync(actor);
            var role = ResolveAuditActorRole(roles);
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

        private static string ResolveAuditActorRole(IList<string> roles)
        {
            if (roles.Count == 0)
            {
                return "Unknown";
            }

            foreach (var prioritizedRole in ActorRolePriority)
            {
                var match = roles.FirstOrDefault(role =>
                    string.Equals(role, prioritizedRole, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(match))
                {
                    return match;
                }
            }

            return roles
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()
                ?? "Unknown";
        }
    }
}
