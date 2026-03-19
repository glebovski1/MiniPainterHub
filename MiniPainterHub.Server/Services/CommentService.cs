using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public class CommentService : ICommentService
    {
        private const int MaxCommentLength = 4000;

        private readonly AppDbContext _appDbContext;
        private readonly CommentMarkService _commentMarkService;
        private readonly IAccountRestrictionService? _accountRestrictionService;
        public CommentService(
            AppDbContext appDbContext,
            IAccountRestrictionService? accountRestrictionService = null,
            CommentMarkService? commentMarkService = null)
        {
            _appDbContext = appDbContext;
            _accountRestrictionService = accountRestrictionService;
            _commentMarkService = commentMarkService ?? new CommentMarkService(appDbContext);
        }
        public async Task<CommentDto> CreateAsync(string userId, int postId, CreateCommentDto dto)
        {
            if (_accountRestrictionService != null)
            {
                await _accountRestrictionService.EnsureCanCommentAsync(userId);
            }

            var text = ValidateAndNormalizeText(dto.Text, "text");

            var postExists = await _appDbContext.Posts.AnyAsync(p => p.Id == postId && !p.IsDeleted);
            if (!postExists)
                throw new NotFoundException("Post not found.");

            var comment = new Comment
            {
                AuthorId = userId,
                PostId = postId,
                Text = text,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };

            comment.ViewerMark = await _commentMarkService.CreateDraftAsync(comment, dto.Mark);

            _appDbContext.Comments.Add(comment);
            await _appDbContext.SaveChangesAsync();

            return new CommentDto
            {
                Id = comment.Id,
                AuthorId = comment.AuthorId,
                PostId = comment.PostId,
                Content = comment.Text,
                CreatedAt = comment.CreatedUtc,
                IsDeleted = comment.IsDeleted,
                HasViewerMark = comment.ViewerMark is not null,
                MarkedPostImageId = comment.ViewerMark?.PostImageId
            };

        }

        public async Task<bool> DeleteAsync(int commentId, string userId, bool isAdmin)
        {
            var comment = await _appDbContext.Comments
                .FirstOrDefaultAsync(c =>
                    c.Id == commentId
                    && (c.AuthorId == userId || isAdmin)
                    && !c.IsDeleted
                );

            if (comment == null)
                throw new NotFoundException("Comment not found.");

            comment.IsDeleted = true;
            comment.SoftDeletedUtc = DateTime.UtcNow;
            comment.UpdatedUtc = DateTime.UtcNow;
            await _appDbContext.SaveChangesAsync();
            return true;
        }

        public async Task<CommentDto> GetByIdAsync(int id)
        {
            var comment = await _appDbContext.Comments
                .AsNoTracking()
                .Where(c => c.Id == id && !c.IsDeleted)
                .Select(c => new CommentDto
                {
                    Id = c.Id,
                    AuthorId = c.AuthorId,
                    AuthorName = c.Author.UserName ?? "No name",
                    PostId = c.PostId,
                    Content = c.Text,
                    CreatedAt = c.CreatedUtc,
                    IsDeleted = c.IsDeleted,
                    HasViewerMark = c.ViewerMark != null,
                    MarkedPostImageId = c.ViewerMark != null ? c.ViewerMark.PostImageId : (int?)null
                })
                .FirstOrDefaultAsync();

            if (comment == null)
            {
                throw new NotFoundException("Comment not found.");
            }

            return comment;
        }

        public async Task<PagedResult<CommentDto>> GetByPostIdAsync(int postId, int page = 1, int pageSize = 10, bool includeDeleted = false, bool deletedOnly = false)
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

            var query = _appDbContext.Comments
                .AsNoTracking()
                .Where(c => c.PostId == postId);

            if (deletedOnly)
            {
                query = query.Where(c => c.IsDeleted);
            }
            else if (!includeDeleted)
            {
                query = query.Where(c => !c.IsDeleted);
            }

            query = query.OrderBy(c => c.CreatedUtc);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CommentDto
                {
                    Id = c.Id,
                    AuthorId = c.AuthorId,
                    PostId = c.PostId,
                    Content = c.Text,
                    CreatedAt = c.CreatedUtc,
                    AuthorName = c.Author.UserName ?? "No name",
                    IsDeleted = c.IsDeleted,
                    HasViewerMark = c.ViewerMark != null,
                    MarkedPostImageId = c.ViewerMark != null ? c.ViewerMark.PostImageId : (int?)null
                })
                .ToListAsync();

            return new PagedResult<CommentDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize


            };
        }

        public async Task<bool> UpdateAsync(int commentId, string userId, UpdateCommentDto dto)
        {
            var text = ValidateAndNormalizeText(dto.Content, "content");

            var comment = await _appDbContext.Comments
                .FirstOrDefaultAsync(c => c.Id == commentId && c.AuthorId == userId && !c.IsDeleted);
            if (comment == null)
                throw new NotFoundException("Comment not found.");

            comment.Text = text;
            comment.UpdatedUtc = DateTime.UtcNow;
            await _appDbContext.SaveChangesAsync();
            return true;
        }

        private static string ValidateAndNormalizeText(string? value, string fieldName)
        {
            var normalized = value?.Trim() ?? string.Empty;
            var errors = new Dictionary<string, string[]>();

            if (normalized.Length == 0)
            {
                errors[fieldName] = new[] { "Comment text is required." };
            }
            else if (normalized.Length > MaxCommentLength)
            {
                errors[fieldName] = new[] { $"Comment text must be {MaxCommentLength} characters or fewer." };
            }

            if (errors.Count > 0)
            {
                throw new DomainValidationException("Comment data is invalid.", errors);
            }

            return normalized;
        }
    }
}
