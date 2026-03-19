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
    public class CommentMarkService : ICommentMarkService
    {
        private readonly AppDbContext _appDbContext;

        public CommentMarkService(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public async Task<CommentMarkDto> GetByCommentIdAsync(int commentId, bool includeDeleted = false)
        {
            var query = _appDbContext.CommentImageMarks
                .AsNoTracking()
                .Where(m => m.CommentId == commentId && !m.Comment.Post.IsDeleted);

            if (!includeDeleted)
            {
                query = query.Where(m => !m.Comment.IsDeleted);
            }

            var mark = await query
                .Select(m => new CommentMarkDto
                {
                    CommentId = m.CommentId,
                    PostImageId = m.PostImageId,
                    NormalizedX = m.NormalizedX,
                    NormalizedY = m.NormalizedY
                })
                .FirstOrDefaultAsync();

            if (mark is null)
            {
                throw new NotFoundException("Comment mark not found.");
            }

            return mark;
        }

        public async Task<CommentMarkDto> UpsertAsync(int commentId, string userId, ViewerMarkDraftDto dto)
        {
            var comment = await _appDbContext.Comments
                .Include(c => c.ViewerMark)
                .FirstOrDefaultAsync(c => c.Id == commentId && c.AuthorId == userId && !c.IsDeleted);

            if (comment is null)
            {
                throw new NotFoundException("Comment not found.");
            }

            var postImage = await GetPostImageForCommentAsync(comment.PostId, dto.PostImageId, "PostImageId");
            var (normalizedX, normalizedY) = ViewerMarkValidation.NormalizeCoordinates(dto.NormalizedX, dto.NormalizedY);

            if (comment.ViewerMark is null)
            {
                comment.ViewerMark = new CommentImageMark
                {
                    CommentId = comment.Id,
                    PostImageId = postImage.Id,
                    NormalizedX = normalizedX,
                    NormalizedY = normalizedY,
                    CreatedUtc = DateTime.UtcNow,
                    UpdatedUtc = DateTime.UtcNow
                };
            }
            else
            {
                comment.ViewerMark.PostImageId = postImage.Id;
                comment.ViewerMark.NormalizedX = normalizedX;
                comment.ViewerMark.NormalizedY = normalizedY;
                comment.ViewerMark.UpdatedUtc = DateTime.UtcNow;
            }

            await _appDbContext.SaveChangesAsync();

            return new CommentMarkDto
            {
                CommentId = comment.Id,
                PostImageId = postImage.Id,
                NormalizedX = normalizedX,
                NormalizedY = normalizedY
            };
        }

        public async Task DeleteAsync(int commentId, string userId)
        {
            var mark = await _appDbContext.CommentImageMarks
                .Include(m => m.Comment)
                .FirstOrDefaultAsync(m => m.CommentId == commentId && m.Comment.AuthorId == userId && !m.Comment.IsDeleted);

            if (mark is null)
            {
                throw new NotFoundException("Comment mark not found.");
            }

            _appDbContext.CommentImageMarks.Remove(mark);
            await _appDbContext.SaveChangesAsync();
        }

        internal async Task<CommentImageMark?> CreateDraftAsync(Comment comment, ViewerMarkDraftDto? draft)
        {
            if (draft is null)
            {
                return null;
            }

            var postImage = await GetPostImageForCommentAsync(comment.PostId, draft.PostImageId, "Mark.PostImageId");
            var (normalizedX, normalizedY) = ViewerMarkValidation.NormalizeCoordinates(draft.NormalizedX, draft.NormalizedY, "Mark.NormalizedX", "Mark.NormalizedY");

            return new CommentImageMark
            {
                Comment = comment,
                PostImageId = postImage.Id,
                NormalizedX = normalizedX,
                NormalizedY = normalizedY,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };
        }

        private async Task<PostImage> GetPostImageForCommentAsync(int postId, int postImageId, string fieldName)
        {
            var postImage = await _appDbContext.PostImages
                .FirstOrDefaultAsync(i => i.Id == postImageId && i.PostId == postId);

            if (postImage is null)
            {
                throw new DomainValidationException("Viewer mark data is invalid.", new Dictionary<string, string[]>
                {
                    [fieldName] = new[] { "Image was not found for this comment's post." }
                });
            }

            return postImage;
        }
    }
}
