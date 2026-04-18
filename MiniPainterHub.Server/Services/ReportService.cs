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
    public sealed class ReportService : IReportService
    {
        private const int MaxDetailsLength = 1000;
        private const int MaxResolutionNoteLength = 500;
        private readonly AppDbContext _appDbContext;

        public ReportService(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public Task SubmitPostReportAsync(string reporterUserId, int postId, CreateReportRequestDto request) =>
            SubmitAsync(reporterUserId, ReportTargetTypes.Post, postId.ToString(), request);

        public Task SubmitCommentReportAsync(string reporterUserId, int commentId, CreateReportRequestDto request) =>
            SubmitAsync(reporterUserId, ReportTargetTypes.Comment, commentId.ToString(), request);

        public Task SubmitUserReportAsync(string reporterUserId, string targetUserId, CreateReportRequestDto request) =>
            SubmitAsync(reporterUserId, ReportTargetTypes.User, targetUserId, request);

        public async Task<PagedResult<ReportQueueItemDto>> GetQueueAsync(ReportQueueQueryDto query)
        {
            ValidatePaging(query.Page, query.PageSize);
            var status = NormalizeOptional(query.Status);
            var targetType = NormalizeOptional(query.TargetType);
            var reasonCode = NormalizeOptional(query.ReasonCode);

            IQueryable<ContentReport> reports = _appDbContext.ContentReports.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(status))
            {
                reports = reports.Where(r => r.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(targetType))
            {
                reports = reports.Where(r => r.TargetType == targetType);
            }

            if (!string.IsNullOrWhiteSpace(reasonCode))
            {
                reports = reports.Where(r => r.ReasonCode == reasonCode);
            }

            reports = reports.OrderByDescending(r => r.CreatedUtc);

            var totalCount = await reports.CountAsync();
            var pageItems = await reports
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            var reporterIds = pageItems.Select(r => r.ReporterUserId);
            var reviewerIds = pageItems.Where(r => !string.IsNullOrWhiteSpace(r.ReviewedByUserId)).Select(r => r.ReviewedByUserId!);
            var userIds = reporterIds.Concat(reviewerIds).Distinct(StringComparer.Ordinal).ToList();
            var users = await _appDbContext.Users
                .AsNoTracking()
                .Include(u => u.Profile)
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, ResolveDisplayName);

            var postIds = pageItems
                .Where(r => r.TargetType == ReportTargetTypes.Post && int.TryParse(r.TargetId, out _))
                .Select(r => int.Parse(r.TargetId))
                .Distinct()
                .ToList();
            var commentIds = pageItems
                .Where(r => r.TargetType == ReportTargetTypes.Comment && int.TryParse(r.TargetId, out _))
                .Select(r => int.Parse(r.TargetId))
                .Distinct()
                .ToList();
            var targetUserIds = pageItems
                .Where(r => r.TargetType == ReportTargetTypes.User)
                .Select(r => r.TargetId)
                .Distinct()
                .ToList();

            var posts = await _appDbContext.Posts
                .AsNoTracking()
                .Where(p => postIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);
            var comments = await _appDbContext.Comments
                .AsNoTracking()
                .Where(c => commentIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);
            var targetUsers = await _appDbContext.Users
                .AsNoTracking()
                .Include(u => u.Profile)
                .Where(u => targetUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, ResolveDisplayName);

            var items = pageItems
                .Select(r => MapQueueItem(r, users, posts, comments, targetUsers))
                .ToList();

            return new PagedResult<ReportQueueItemDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task ResolveAsync(string reviewerUserId, long reportId, ResolveReportRequestDto request)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateResolution(request);

            var report = await _appDbContext.ContentReports.FirstOrDefaultAsync(r => r.Id == reportId);
            if (report is null)
            {
                throw new NotFoundException("Report not found.");
            }

            if (!string.Equals(report.Status, ReportStatuses.Open, StringComparison.Ordinal))
            {
                throw new DomainValidationException("Report is already resolved.", new Dictionary<string, string[]>
                {
                    ["status"] = new[] { "Only open reports can be resolved." }
                });
            }

            report.Status = request.Status.Trim();
            report.ResolutionNote = NormalizeOptional(request.ResolutionNote);
            report.ReviewedByUserId = reviewerUserId;
            report.ReviewedUtc = DateTime.UtcNow;
            report.UpdatedUtc = report.ReviewedUtc.Value;

            await _appDbContext.SaveChangesAsync();
        }

        private async Task SubmitAsync(string reporterUserId, string targetType, string targetId, CreateReportRequestDto request)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateCreateRequest(request);

            await EnsureTargetExistsAsync(reporterUserId, targetType, targetId);

            var duplicateExists = await _appDbContext.ContentReports.AnyAsync(r =>
                r.ReporterUserId == reporterUserId
                && r.TargetType == targetType
                && r.TargetId == targetId
                && r.Status == ReportStatuses.Open);

            if (duplicateExists)
            {
                throw new DomainValidationException("Report already exists.", new Dictionary<string, string[]>
                {
                    ["report"] = new[] { "You already have an open report for this item." }
                });
            }

            _appDbContext.ContentReports.Add(new ContentReport
            {
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                ReporterUserId = reporterUserId,
                TargetType = targetType,
                TargetId = targetId,
                ReasonCode = request.ReasonCode.Trim(),
                Details = NormalizeOptional(request.Details),
                Status = ReportStatuses.Open
            });

            await _appDbContext.SaveChangesAsync();
        }

        private async Task EnsureTargetExistsAsync(string reporterUserId, string targetType, string targetId)
        {
            switch (targetType)
            {
                case ReportTargetTypes.Post:
                    {
                        if (!int.TryParse(targetId, out var postId))
                        {
                            throw new NotFoundException("Post not found.");
                        }

                        var post = await _appDbContext.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == postId);
                        if (post is null)
                        {
                            throw new NotFoundException("Post not found.");
                        }

                        if (string.Equals(post.CreatedById, reporterUserId, StringComparison.Ordinal))
                        {
                            throw new ForbiddenException("You cannot report your own post.");
                        }

                        break;
                    }
                case ReportTargetTypes.Comment:
                    {
                        if (!int.TryParse(targetId, out var commentId))
                        {
                            throw new NotFoundException("Comment not found.");
                        }

                        var comment = await _appDbContext.Comments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == commentId);
                        if (comment is null)
                        {
                            throw new NotFoundException("Comment not found.");
                        }

                        if (string.Equals(comment.AuthorId, reporterUserId, StringComparison.Ordinal))
                        {
                            throw new ForbiddenException("You cannot report your own comment.");
                        }

                        break;
                    }
                case ReportTargetTypes.User:
                    {
                        var user = await _appDbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == targetId);
                        if (user is null)
                        {
                            throw new NotFoundException("User not found.");
                        }

                        if (string.Equals(targetId, reporterUserId, StringComparison.Ordinal))
                        {
                            throw new ForbiddenException("You cannot report your own profile.");
                        }

                        break;
                    }
                default:
                    throw new DomainValidationException("Unsupported report target.", new Dictionary<string, string[]>
                    {
                        ["target"] = new[] { "Unsupported report target." }
                    });
            }
        }

        private static void ValidateCreateRequest(CreateReportRequestDto request)
        {
            var reasonCode = request.ReasonCode?.Trim();
            if (string.IsNullOrWhiteSpace(reasonCode) || !ReportReasonCodes.All.Contains(reasonCode, StringComparer.Ordinal))
            {
                throw new DomainValidationException("Report request is invalid.", new Dictionary<string, string[]>
                {
                    ["reasonCode"] = new[] { "Provide a valid report reason." }
                });
            }

            if (string.Equals(reasonCode, ReportReasonCodes.Other, StringComparison.Ordinal) && string.IsNullOrWhiteSpace(request.Details))
            {
                throw new DomainValidationException("Report request is invalid.", new Dictionary<string, string[]>
                {
                    ["details"] = new[] { "Details are required when reporting as Other." }
                });
            }

            if (!string.IsNullOrWhiteSpace(request.Details) && request.Details.Trim().Length > MaxDetailsLength)
            {
                throw new DomainValidationException("Report request is invalid.", new Dictionary<string, string[]>
                {
                    ["details"] = new[] { $"Details must be {MaxDetailsLength} characters or fewer." }
                });
            }
        }

        private static void ValidateResolution(ResolveReportRequestDto request)
        {
            var status = request.Status?.Trim();
            if (string.IsNullOrWhiteSpace(status) || !ReportStatuses.Resolvable.Contains(status, StringComparer.Ordinal))
            {
                throw new DomainValidationException("Report resolution is invalid.", new Dictionary<string, string[]>
                {
                    ["status"] = new[] { "Provide a valid resolution status." }
                });
            }

            if (!string.IsNullOrWhiteSpace(request.ResolutionNote) && request.ResolutionNote.Trim().Length > MaxResolutionNoteLength)
            {
                throw new DomainValidationException("Report resolution is invalid.", new Dictionary<string, string[]>
                {
                    ["resolutionNote"] = new[] { $"Resolution note must be {MaxResolutionNoteLength} characters or fewer." }
                });
            }
        }

        private static string? NormalizeOptional(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static ReportQueueItemDto MapQueueItem(
            ContentReport report,
            IReadOnlyDictionary<string, string> users,
            IReadOnlyDictionary<int, Post> posts,
            IReadOnlyDictionary<int, Comment> comments,
            IReadOnlyDictionary<string, string> targetUsers)
        {
            var targetSummary = report.TargetType switch
            {
                ReportTargetTypes.Post when int.TryParse(report.TargetId, out var postId) && posts.TryGetValue(postId, out var post)
                    => post.Title,
                ReportTargetTypes.Comment when int.TryParse(report.TargetId, out var commentId) && comments.TryGetValue(commentId, out var comment)
                    => comment.Text.Length > 120 ? comment.Text[..120] + "..." : comment.Text,
                ReportTargetTypes.User when targetUsers.TryGetValue(report.TargetId, out var userDisplay)
                    => userDisplay,
                _ => "Target unavailable"
            };

            var targetUrl = report.TargetType switch
            {
                ReportTargetTypes.Post => $"/posts/{report.TargetId}",
                ReportTargetTypes.Comment when int.TryParse(report.TargetId, out var commentId) && comments.TryGetValue(commentId, out var comment)
                    => $"/posts/{comment.PostId}",
                ReportTargetTypes.User => $"/users/{report.TargetId}",
                _ => "/"
            };

            users.TryGetValue(report.ReporterUserId, out var reporterName);
            if (!string.IsNullOrWhiteSpace(report.ReviewedByUserId))
            {
                users.TryGetValue(report.ReviewedByUserId, out var reviewerName);
                return new ReportQueueItemDto
                {
                    Id = report.Id,
                    CreatedUtc = report.CreatedUtc,
                    ReviewedUtc = report.ReviewedUtc,
                    Status = report.Status,
                    TargetType = report.TargetType,
                    TargetId = report.TargetId,
                    ReasonCode = report.ReasonCode,
                    Details = report.Details,
                    ReporterUserId = report.ReporterUserId,
                    ReporterUserName = reporterName ?? report.ReporterUserId,
                    ReviewedByUserId = report.ReviewedByUserId,
                    ReviewedByUserName = reviewerName,
                    TargetSummary = targetSummary,
                    TargetUrl = targetUrl,
                    ResolutionNote = report.ResolutionNote
                };
            }

            return new ReportQueueItemDto
            {
                Id = report.Id,
                CreatedUtc = report.CreatedUtc,
                ReviewedUtc = report.ReviewedUtc,
                Status = report.Status,
                TargetType = report.TargetType,
                TargetId = report.TargetId,
                ReasonCode = report.ReasonCode,
                Details = report.Details,
                ReporterUserId = report.ReporterUserId,
                ReporterUserName = reporterName ?? report.ReporterUserId,
                TargetSummary = targetSummary,
                TargetUrl = targetUrl,
                ResolutionNote = report.ResolutionNote
            };
        }

        private static string ResolveDisplayName(ApplicationUser user) =>
            user.Profile?.DisplayName
            ?? user.DisplayName
            ?? user.UserName
            ?? user.Id;

        private static void ValidatePaging(int page, int pageSize)
        {
            var errors = new Dictionary<string, string[]>();
            if (page < 1)
            {
                errors["page"] = new[] { "Page number must be at least 1." };
            }

            if (pageSize <= 0)
            {
                errors["pageSize"] = new[] { "Page size must be greater than 0." };
            }

            if (errors.Count > 0)
            {
                throw new DomainValidationException("Pagination parameters are invalid.", errors);
            }
        }
    }
}
