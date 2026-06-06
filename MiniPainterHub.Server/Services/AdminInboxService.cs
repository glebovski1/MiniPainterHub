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
    public sealed class AdminInboxService : IAdminInboxService
    {
        private const int MaxPageSize = 100;
        private const int MaxWindowHours = 24 * 30;
        private static readonly string[] ActorRolePriority = { "Admin", "Moderator" };

        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminInboxService(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<PagedResult<AdminInboxItemDto>> GetInboxAsync(AdminInboxQueryDto query)
        {
            var normalized = NormalizeQuery(query);
            var cutoff = DateTime.UtcNow.AddHours(-normalized.WindowHours);
            var items = new List<InboxProjection>();

            if (normalized.ItemType is AdminInboxItemTypes.All or AdminInboxItemTypes.Post)
            {
                items.AddRange(await LoadPostItemsAsync(cutoff));
            }

            if (normalized.ItemType is AdminInboxItemTypes.All or AdminInboxItemTypes.Comment)
            {
                items.AddRange(await LoadCommentItemsAsync(cutoff));
            }

            await EnrichItemsAsync(items);

            var filtered = items
                .Where(item => MatchesState(item, normalized.State ?? AdminInboxStates.All))
                .Where(item => MatchesSearch(item, normalized.Search))
                .OrderByDescending(item => item.SortUtc)
                .ThenBy(item => item.TargetType, StringComparer.Ordinal)
                .ThenByDescending(item => item.TargetId, StringComparer.Ordinal)
                .ToList();

            var total = filtered.Count;
            var pageItems = filtered
                .Skip((normalized.Page - 1) * normalized.PageSize)
                .Take(normalized.PageSize)
                .Select(MapItem)
                .ToList();

            return new PagedResult<AdminInboxItemDto>
            {
                Items = pageItems,
                TotalCount = total,
                PageNumber = normalized.Page,
                PageSize = normalized.PageSize
            };
        }

        public async Task<AdminInboxDetailDto> GetDetailAsync(string targetType, string targetId)
        {
            EnsureTarget(targetType, targetId, out var numericId);

            if (targetType == AdminInboxItemTypes.Post)
            {
                return await GetPostDetailAsync(numericId);
            }

            return await GetCommentDetailAsync(numericId);
        }

        public async Task ReviewAsync(string targetType, string targetId, string actorUserId, AdminInboxReviewRequestDto request)
        {
            EnsureTarget(targetType, targetId, out var numericId);
            ArgumentNullException.ThrowIfNull(request);

            var targetExists = targetType == AdminInboxItemTypes.Post
                ? await _db.Posts.AnyAsync(p => p.Id == numericId)
                : await _db.Comments.AnyAsync(c => c.Id == numericId);

            if (!targetExists)
            {
                throw new NotFoundException($"{targetType} not found.");
            }

            var now = DateTime.UtcNow;
            var openReports = await _db.ContentReports
                .Where(r => r.TargetType == targetType
                    && r.TargetId == targetId
                    && r.Status == ReportStatuses.Open)
                .ToListAsync();

            foreach (var report in openReports)
            {
                report.Status = ReportStatuses.Reviewed;
                report.ReviewedByUserId = actorUserId;
                report.ReviewedUtc = now;
                report.UpdatedUtc = now;
                report.ResolutionNote = NormalizeOptional(request.Reason) ?? "Reviewed from admin inbox.";
            }

            await AddAuditAsync(
                actorUserId,
                targetType == AdminInboxItemTypes.Post ? "PostReviewed" : "CommentReviewed",
                targetType,
                targetId,
                request.Reason);

            await _db.SaveChangesAsync();
        }

        private async Task<IReadOnlyList<InboxProjection>> LoadPostItemsAsync(DateTime cutoff)
        {
            var posts = await _db.Posts
                .AsNoTracking()
                .Include(p => p.CreatedBy)
                .ThenInclude(u => u.Profile)
                .Where(p => p.CreatedUtc >= cutoff || p.UpdatedUtc >= cutoff || p.ModeratedUtc >= cutoff)
                .ToListAsync();

            return posts
                .Select(p => new InboxProjection
                {
                    TargetType = AdminInboxItemTypes.Post,
                    TargetId = p.Id.ToString(),
                    CreatedUtc = p.CreatedUtc,
                    UpdatedUtc = p.UpdatedUtc,
                    SortUtc = p.UpdatedUtc > p.CreatedUtc ? p.UpdatedUtc : p.CreatedUtc,
                    AuthorUserId = p.CreatedById,
                    AuthorName = ResolveDisplayName(p.CreatedBy),
                    Summary = p.Title,
                    ContextSummary = Snippet(p.Content, 140),
                    TargetUrl = $"/posts/{p.Id}",
                    IsDeleted = p.IsDeleted,
                    ModeratedUtc = p.ModeratedUtc,
                    ModerationReason = p.ModerationReason
                })
                .ToList();
        }

        private async Task<IReadOnlyList<InboxProjection>> LoadCommentItemsAsync(DateTime cutoff)
        {
            var comments = await _db.Comments
                .AsNoTracking()
                .Include(c => c.Author)
                .ThenInclude(u => u.Profile)
                .Include(c => c.Post)
                .Where(c => c.CreatedUtc >= cutoff || c.UpdatedUtc >= cutoff || c.ModeratedUtc >= cutoff)
                .ToListAsync();

            return comments
                .Select(c => new InboxProjection
                {
                    TargetType = AdminInboxItemTypes.Comment,
                    TargetId = c.Id.ToString(),
                    CreatedUtc = c.CreatedUtc,
                    UpdatedUtc = c.UpdatedUtc,
                    SortUtc = c.UpdatedUtc > c.CreatedUtc ? c.UpdatedUtc : c.CreatedUtc,
                    AuthorUserId = c.AuthorId,
                    AuthorName = ResolveDisplayName(c.Author),
                    Summary = Snippet(c.Text, 160),
                    ContextSummary = c.Post.Title,
                    TargetUrl = $"/posts/{c.PostId}",
                    IsDeleted = c.IsDeleted,
                    ModeratedUtc = c.ModeratedUtc,
                    ModerationReason = c.ModerationReason
                })
                .ToList();
        }

        private async Task EnrichItemsAsync(IReadOnlyList<InboxProjection> items)
        {
            if (items.Count == 0)
            {
                return;
            }

            var targetTypes = items.Select(i => i.TargetType).Distinct(StringComparer.Ordinal).ToList();
            var targetIds = items.Select(i => i.TargetId).Distinct(StringComparer.Ordinal).ToList();
            var reports = await _db.ContentReports
                .AsNoTracking()
                .Where(r => targetTypes.Contains(r.TargetType) && targetIds.Contains(r.TargetId))
                .GroupBy(r => new { r.TargetType, r.TargetId })
                .Select(g => new
                {
                    g.Key.TargetType,
                    g.Key.TargetId,
                    Total = g.Count(),
                    Open = g.Count(r => r.Status == ReportStatuses.Open),
                    LatestUtc = g.Max(r => r.CreatedUtc)
                })
                .ToListAsync();
            var reportLookup = reports.ToDictionary(r => Key(r.TargetType, r.TargetId), StringComparer.Ordinal);

            var reviewActions = new[] { "PostReviewed", "CommentReviewed" };
            var reviews = await _db.ModerationAuditLogs
                .AsNoTracking()
                .Where(a => targetTypes.Contains(a.TargetType)
                    && targetIds.Contains(a.TargetId)
                    && reviewActions.Contains(a.ActionType))
                .GroupBy(a => new { a.TargetType, a.TargetId })
                .Select(g => new { g.Key.TargetType, g.Key.TargetId, LatestUtc = g.Max(a => a.CreatedUtc) })
                .ToListAsync();
            var reviewLookup = reviews.ToDictionary(r => Key(r.TargetType, r.TargetId), StringComparer.Ordinal);

            foreach (var item in items)
            {
                if (reportLookup.TryGetValue(Key(item.TargetType, item.TargetId), out var report))
                {
                    item.TotalReportCount = report.Total;
                    item.OpenReportCount = report.Open;
                    item.LatestReportUtc = report.LatestUtc;
                    item.SortUtc = MaxUtc(item.SortUtc, report.LatestUtc);
                }

                if (reviewLookup.TryGetValue(Key(item.TargetType, item.TargetId), out var review))
                {
                    item.HasBeenReviewed = true;
                    item.SortUtc = MaxUtc(item.SortUtc, review.LatestUtc);
                }

                item.State = ResolveState(item);
            }
        }

        private async Task<AdminInboxDetailDto> GetPostDetailAsync(int postId)
        {
            var post = await _db.Posts
                .AsNoTracking()
                .Include(p => p.CreatedBy)
                .ThenInclude(u => u.Profile)
                .Where(p => p.Id == postId)
                .Select(p => new
                {
                    Post = p,
                    Author = p.CreatedBy
                })
                .FirstOrDefaultAsync();

            if (post is null)
            {
                throw new NotFoundException("Post not found.");
            }

            var reports = await LoadReportsAsync(AdminInboxItemTypes.Post, postId.ToString());
            var reviewed = await HasReviewedAuditAsync(AdminInboxItemTypes.Post, postId.ToString());
            return new AdminInboxDetailDto
            {
                TargetType = AdminInboxItemTypes.Post,
                TargetId = post.Post.Id.ToString(),
                CreatedUtc = post.Post.CreatedUtc,
                UpdatedUtc = post.Post.UpdatedUtc,
                AuthorUserId = post.Post.CreatedById,
                AuthorName = ResolveDisplayName(post.Author),
                AuthorEmail = post.Author.Email,
                AuthorJoinedUtc = post.Author.DateJoined,
                AuthorSuspended = post.Author.SuspendedUntilUtc.HasValue && post.Author.SuspendedUntilUtc.Value > DateTime.UtcNow,
                AuthorSuspendedUntilUtc = post.Author.SuspendedUntilUtc,
                Title = post.Post.Title,
                Body = post.Post.Content,
                ContextSummary = $"Post #{post.Post.Id}",
                TargetUrl = $"/posts/{post.Post.Id}",
                AuditUrl = $"/admin/audit?targetType=Post",
                State = ResolveState(post.Post.IsDeleted, reports.Count(r => r.Status == ReportStatuses.Open), reviewed),
                IsDeleted = post.Post.IsDeleted,
                HasBeenReviewed = reviewed,
                ModeratedUtc = post.Post.ModeratedUtc,
                ModeratedByUserId = post.Post.ModeratedByUserId,
                ModerationReason = post.Post.ModerationReason,
                Reports = reports
            };
        }

        private async Task<AdminInboxDetailDto> GetCommentDetailAsync(int commentId)
        {
            var comment = await _db.Comments
                .AsNoTracking()
                .Include(c => c.Author)
                .ThenInclude(u => u.Profile)
                .Include(c => c.Post)
                .Where(c => c.Id == commentId)
                .Select(c => new
                {
                    Comment = c,
                    Author = c.Author,
                    Post = c.Post
                })
                .FirstOrDefaultAsync();

            if (comment is null)
            {
                throw new NotFoundException("Comment not found.");
            }

            var reports = await LoadReportsAsync(AdminInboxItemTypes.Comment, commentId.ToString());
            var reviewed = await HasReviewedAuditAsync(AdminInboxItemTypes.Comment, commentId.ToString());
            return new AdminInboxDetailDto
            {
                TargetType = AdminInboxItemTypes.Comment,
                TargetId = comment.Comment.Id.ToString(),
                CreatedUtc = comment.Comment.CreatedUtc,
                UpdatedUtc = comment.Comment.UpdatedUtc,
                AuthorUserId = comment.Comment.AuthorId,
                AuthorName = ResolveDisplayName(comment.Author),
                AuthorEmail = comment.Author.Email,
                AuthorJoinedUtc = comment.Author.DateJoined,
                AuthorSuspended = comment.Author.SuspendedUntilUtc.HasValue && comment.Author.SuspendedUntilUtc.Value > DateTime.UtcNow,
                AuthorSuspendedUntilUtc = comment.Author.SuspendedUntilUtc,
                Body = comment.Comment.Text,
                ContextSummary = comment.Post.Title,
                ParentPostId = comment.Comment.PostId,
                ParentPostTitle = comment.Post.Title,
                TargetUrl = $"/posts/{comment.Comment.PostId}",
                AuditUrl = $"/admin/audit?targetType=Comment",
                State = ResolveState(comment.Comment.IsDeleted, reports.Count(r => r.Status == ReportStatuses.Open), reviewed),
                IsDeleted = comment.Comment.IsDeleted,
                HasBeenReviewed = reviewed,
                ModeratedUtc = comment.Comment.ModeratedUtc,
                ModeratedByUserId = comment.Comment.ModeratedByUserId,
                ModerationReason = comment.Comment.ModerationReason,
                Reports = reports
            };
        }

        private async Task<IReadOnlyList<AdminInboxReportDto>> LoadReportsAsync(string targetType, string targetId)
        {
            var reports = await _db.ContentReports
                .AsNoTracking()
                .Where(r => r.TargetType == targetType && r.TargetId == targetId)
                .OrderByDescending(r => r.CreatedUtc)
                .ToListAsync();
            if (reports.Count == 0)
            {
                return Array.Empty<AdminInboxReportDto>();
            }

            var reporterIds = reports.Select(r => r.ReporterUserId)
                .Concat(reports.Where(r => !string.IsNullOrWhiteSpace(r.ReviewedByUserId)).Select(r => r.ReviewedByUserId!))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var usersList = await _db.Users
                .AsNoTracking()
                .Include(u => u.Profile)
                .Where(u => reporterIds.Contains(u.Id))
                .ToListAsync();
            var users = usersList.ToDictionary(u => u.Id, ResolveDisplayName, StringComparer.Ordinal);

            return reports
                .Select(r => new AdminInboxReportDto
                {
                    Id = r.Id,
                    CreatedUtc = r.CreatedUtc,
                    ReviewedUtc = r.ReviewedUtc,
                    Status = r.Status,
                    ReasonCode = r.ReasonCode,
                    Details = r.Details,
                    ReporterUserId = r.ReporterUserId,
                    ReporterUserName = users.TryGetValue(r.ReporterUserId, out var reporter) ? reporter : r.ReporterUserId,
                    ReviewedByUserId = r.ReviewedByUserId,
                    ResolutionNote = r.ResolutionNote
                })
                .ToList();
        }

        private Task<bool> HasReviewedAuditAsync(string targetType, string targetId)
        {
            var actionType = targetType == AdminInboxItemTypes.Post ? "PostReviewed" : "CommentReviewed";
            return _db.ModerationAuditLogs
                .AsNoTracking()
                .AnyAsync(a => a.TargetType == targetType && a.TargetId == targetId && a.ActionType == actionType);
        }

        private async Task AddAuditAsync(string actorUserId, string actionType, string targetType, string targetId, string? reason)
        {
            var actor = await _userManager.FindByIdAsync(actorUserId) ?? throw new UnauthorizedAccessException("Actor not found.");
            var roles = await _userManager.GetRolesAsync(actor);
            _db.ModerationAuditLogs.Add(new ModerationAuditLog
            {
                CreatedUtc = DateTime.UtcNow,
                ActorUserId = actorUserId,
                ActorRole = ResolveAuditActorRole(roles),
                ActionType = actionType,
                TargetType = targetType,
                TargetId = targetId,
                Reason = NormalizeOptional(reason)
            });
        }

        private static AdminInboxQueryDto NormalizeQuery(AdminInboxQueryDto? query)
        {
            query ??= new AdminInboxQueryDto();
            var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            if (query.Page < 1)
            {
                errors["page"] = new[] { "Page number must be at least 1." };
            }

            if (query.PageSize < 1 || query.PageSize > MaxPageSize)
            {
                errors["pageSize"] = new[] { $"Page size must be between 1 and {MaxPageSize}." };
            }

            if (query.WindowHours < 1 || query.WindowHours > MaxWindowHours)
            {
                errors["windowHours"] = new[] { $"Window must be between 1 and {MaxWindowHours} hours." };
            }

            var itemType = NormalizeOptional(query.ItemType) ?? AdminInboxItemTypes.All;
            if (itemType.Length > 0 && !AdminInboxItemTypes.IsContentType(itemType))
            {
                errors["itemType"] = new[] { "Item type must be Post or Comment." };
            }

            var state = NormalizeOptional(query.State) ?? AdminInboxStates.All;
            if (!AdminInboxStates.IsKnownState(state))
            {
                errors["state"] = new[] { "State filter is not supported." };
            }

            if (errors.Count > 0)
            {
                throw new DomainValidationException("Admin inbox query is invalid.", errors);
            }

            return new AdminInboxQueryDto
            {
                Page = query.Page,
                PageSize = query.PageSize,
                WindowHours = query.WindowHours,
                ItemType = itemType,
                State = state,
                Search = NormalizeOptional(query.Search)
            };
        }

        private static bool MatchesState(InboxProjection item, string state) => state switch
        {
            AdminInboxStates.All => true,
            AdminInboxStates.Reported => item.OpenReportCount > 0,
            AdminInboxStates.Hidden => item.IsDeleted,
            AdminInboxStates.Reviewed => item.HasBeenReviewed && item.OpenReportCount == 0,
            AdminInboxStates.Active => !item.IsDeleted && item.OpenReportCount == 0,
            _ => true
        };

        private static bool MatchesSearch(InboxProjection item, string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return true;
            }

            return Contains(item.Summary, search)
                || Contains(item.ContextSummary, search)
                || Contains(item.AuthorName, search)
                || Contains(item.AuthorUserId, search)
                || Contains(item.TargetId, search);
        }

        private static AdminInboxItemDto MapItem(InboxProjection item) => new()
        {
            TargetType = item.TargetType,
            TargetId = item.TargetId,
            CreatedUtc = item.CreatedUtc,
            UpdatedUtc = item.UpdatedUtc,
            AuthorUserId = item.AuthorUserId,
            AuthorName = item.AuthorName,
            Summary = item.Summary,
            ContextSummary = item.ContextSummary,
            TargetUrl = item.TargetUrl,
            State = item.State,
            IsDeleted = item.IsDeleted,
            HasBeenReviewed = item.HasBeenReviewed,
            OpenReportCount = item.OpenReportCount,
            TotalReportCount = item.TotalReportCount,
            LatestReportUtc = item.LatestReportUtc,
            ModeratedUtc = item.ModeratedUtc,
            ModerationReason = item.ModerationReason
        };

        private static void EnsureTarget(string targetType, string targetId, out int numericId)
        {
            if (!AdminInboxItemTypes.IsContentType(targetType) || !int.TryParse(targetId, out numericId) || numericId <= 0)
            {
                throw new DomainValidationException("Unsupported inbox target.", new Dictionary<string, string[]>
                {
                    ["target"] = new[] { "Target must be a post or comment with a numeric id." }
                });
            }
        }

        private static string ResolveState(InboxProjection item) =>
            ResolveState(item.IsDeleted, item.OpenReportCount, item.HasBeenReviewed);

        private static string ResolveState(bool isDeleted, int openReportCount, bool hasBeenReviewed)
        {
            if (openReportCount > 0)
            {
                return AdminInboxStates.Reported;
            }

            if (isDeleted)
            {
                return AdminInboxStates.Hidden;
            }

            return hasBeenReviewed ? AdminInboxStates.Reviewed : AdminInboxStates.Active;
        }

        private static string? NormalizeOptional(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static bool Contains(string? source, string search) =>
            source?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;

        private static string Snippet(string? value, int maxLength)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
        }

        private static DateTime MaxUtc(DateTime left, DateTime right) => left >= right ? left : right;

        private static string Key(string targetType, string targetId) => $"{targetType}:{targetId}";

        private static string ResolveDisplayName(ApplicationUser user) =>
            user.Profile?.DisplayName
            ?? user.DisplayName
            ?? user.UserName
            ?? user.Id;

        private static string ResolveAuditActorRole(IList<string> roles)
        {
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

        private sealed class InboxProjection
        {
            public string TargetType { get; init; } = default!;
            public string TargetId { get; init; } = default!;
            public DateTime CreatedUtc { get; init; }
            public DateTime UpdatedUtc { get; init; }
            public DateTime SortUtc { get; set; }
            public string AuthorUserId { get; init; } = default!;
            public string AuthorName { get; init; } = default!;
            public string Summary { get; init; } = default!;
            public string? ContextSummary { get; init; }
            public string TargetUrl { get; init; } = default!;
            public string State { get; set; } = AdminInboxStates.Active;
            public bool IsDeleted { get; init; }
            public bool HasBeenReviewed { get; set; }
            public int OpenReportCount { get; set; }
            public int TotalReportCount { get; set; }
            public DateTime? LatestReportUtc { get; set; }
            public DateTime? ModeratedUtc { get; init; }
            public string? ModerationReason { get; init; }
        }
    }
}
