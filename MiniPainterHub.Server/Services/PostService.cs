using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Services.Interfaces;
using System;
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
                CreatedBy = user,
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
                CreatedAt = newPost.CreatedAt
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

        public async Task<PagedResult<PostDto>> GetAllAsync(int page, int pageSize)
        {
            var query = _appDbContext.Posts
                .AsNoTracking()
                .OrderByDescending(p => p.CreatedAt);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PostDto
                {
                    Id = p.Id,
                    CreatedById = p.CreatedById,
                    Title = p.Title,
                    Content = p.Content,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            return new PagedResult<PostDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        public async Task<PostDto> GetByIdAsync(int postId)
        {
            var p = await _appDbContext.Posts
               .AsNoTracking()
               .Include(p => p.Likes)
               .Include(p => p.Comments)
               .FirstOrDefaultAsync(p => p.Id == postId);

            if (p == null)
                return null;

            return new PostDto
            {
                Id = p.Id,
                CreatedById = p.CreatedById,
                Title = p.Title,
                Content = p.Content,
                CreatedAt = p.CreatedAt
            };
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

    }
}
