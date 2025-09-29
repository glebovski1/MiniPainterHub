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
        private AppDbContext _appDbContext;
        public CommentService(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }
        public async Task<CommentDto> CreateAsync(string userId, int postId, CreateCommentDto dto)
        {
            // 1️⃣ Optional: verify the post actually exists
            var postExists = await _appDbContext.Posts.AnyAsync(p => p.Id == postId && !p.IsDeleted);
            if (!postExists)
                throw new NotFoundException("Post not found.");

            // 2️⃣ Create the Comment entity, setting only the FKs
            var comment = new Comment
            {
                AuthorId = userId,      // FK for the user
                PostId = postId,      // FK for the post
                Text = dto.Text,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };

            _appDbContext.Comments.Add(comment);
            await _appDbContext.SaveChangesAsync();

            // 3️⃣ Map to DTO and return
            return new CommentDto
            {
                Id = comment.Id,
                AuthorId = comment.AuthorId,
                PostId = comment.PostId,
                Content = comment.Text,
                CreatedAt = comment.CreatedUtc
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
                    CreatedAt = c.CreatedUtc
                })
                .FirstOrDefaultAsync();

            if (comment == null)
            {
                throw new NotFoundException("Comment not found.");
            }

            return comment;
        }

        public async Task<PagedResult<CommentDto>> GetByPostIdAsync(int postId, int page = 1, int pageSize = 10)
        {
            var query = _appDbContext.Comments
                .AsNoTracking()
                .Where(c => c.PostId == postId && !c.IsDeleted)
                .OrderBy(c => c.CreatedUtc);

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
                    AuthorName = c.Author.UserName ?? "No name"
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
            var comment = await _appDbContext.Comments
                .FirstOrDefaultAsync(c => c.Id == commentId && c.AuthorId == userId && !c.IsDeleted);
            if (comment == null)
                throw new NotFoundException("Comment not found.");

            comment.Text = dto.Content;
            comment.UpdatedUtc = DateTime.UtcNow;
            await _appDbContext.SaveChangesAsync();
            return true;
        }
    }
}
