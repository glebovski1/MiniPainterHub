using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public class PostService : IPostService
    {
        private AppDbContext _appDbContext;
        public PostService(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;

        }
        public async Task<PostDto> CreateAsync(string userId, CreatePostDto dto)
        {
            var user = await _appDbContext.Users.FindAsync(userId);
            // 1️⃣ Create the entity and set the FK
            var newPost = new Post
            {
                CreatedById = userId,
                Title = dto.Title,
                Content = dto.Content,
                CreatedAt = DateTime.UtcNow
            };

            // 2️⃣ Add and save
            _appDbContext.Posts.Add(newPost);
            await _appDbContext.SaveChangesAsync();

            // 3️⃣ Map to DTO and return
            return new PostDto
            {
                Id = newPost.Id,
                CreatedById = userId,
                Title = newPost.Title,
                Content = newPost.Content,
                CreatedAt = newPost.CreatedAt,
                ImageUrl = newPost.ImageUrl,
                AuthorName = user.UserName

            };
        }

        public async Task<bool> DeleteAsync(int postId, string userId)
        {
            // find the post only if it belongs to this user
            var post = await _appDbContext.Posts
                .FirstOrDefaultAsync(p => p.Id == postId && p.CreatedById == userId);

            if (post == null)
                return false;

            _appDbContext.Posts.Remove(post);
            await _appDbContext.SaveChangesAsync();
            return true;
        }

        public async Task<PagedResult<PostSummaryDto>> GetAllAsync(int page, int pageSize)
        {
            var query = _appDbContext.Posts
                .AsNoTracking()
                .OrderByDescending(p => p.CreatedAt);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PostSummaryDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Snippet = p.Content.Length > 100
                                     ? p.Content.Substring(0, 100) + "…"
                                     : p.Content,
                    ImageUrl = p.ImageUrl,
                    AuthorName = p.CreatedBy.UserName,     // or p.CreatedBy.Profile.DisplayName
                    CreatedAt = p.CreatedAt,
                    CommentCount = p.Comments.Count,
                    LikeCount = p.Likes.Count
                })
                .ToListAsync();

            return new PagedResult<PostSummaryDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        public async Task<PostDto> GetByIdAsync(int postId)
        {
              var dto = await _appDbContext.Posts
                     .AsNoTracking()
                     .Where(p => p.Id == postId)
                     .Select(p => new PostDto
                     {
                         Id = p.Id,
                         CreatedById = p.CreatedById,
                         Title = p.Title,
                         Content = p.Content,
                         CreatedAt = p.CreatedAt,
                         ImageUrl = p.ImageUrl,
                         AuthorName = p.CreatedBy.UserName,  // EF will JOIN Users for you
                                                             // …add CommentCount, LikeCount if you want…
                     })
                     .FirstOrDefaultAsync();
            return dto;
        }

        public async Task<bool> UpdateAsync(int postId, string userId, UpdatePostDto dto)
        {
            var post = await _appDbContext.Posts
               .FirstOrDefaultAsync(p => p.Id == postId && p.CreatedById == userId);

            if (post == null)
                return false;

            post.Title = dto.Title;
            post.Content = dto.Content;

            await _appDbContext.SaveChangesAsync();
            return true;
        }

        public async Task SetImageUrlAsync(int postId, string imageUrl)
        {
            var post = await _appDbContext.Posts.FindAsync(postId)
                        ?? throw new KeyNotFoundException("Post not found");
            post.ImageUrl = imageUrl;
            await _appDbContext.SaveChangesAsync();
        }

        public async Task<bool> ExistsAsync(int postId)
        {
            return await _appDbContext.Posts.AnyAsync(post => post.Id == postId);
        }
    }
}
