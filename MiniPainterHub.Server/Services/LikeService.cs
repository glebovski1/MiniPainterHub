using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public class LikeService : ILikeService
    {
        private readonly AppDbContext _appDbContext;

        public LikeService(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }
        // Returns total count and whether specified user has liked
        public async Task<LikeDto> GetLikesAsync(int postId, string? userId)
        {
            var exists = await _appDbContext.Posts.AsNoTracking().AnyAsync(p => p.Id == postId);
            if (!exists)
                throw new KeyNotFoundException($"Post {postId} not found");

            var query = _appDbContext.Likes.AsNoTracking().Where(l => l.PostId == postId);
            var count = await query.CountAsync();
            var isLiked = !string.IsNullOrEmpty(userId) && await query.AnyAsync(l => l.UserId == userId);

            return new LikeDto { PostId = postId, Count = count, UserHasLiked = isLiked };
        }

        // Checks if a specific user has liked the post
        public async Task<bool> IsLikedAsync(int postId, string userId)
        {
            return await _appDbContext.Likes
                .AsNoTracking()
                .AnyAsync(l => l.PostId == postId && l.UserId == userId);
        }


        // Toggles like: adds if not present, removes if already liked
        public async Task<bool> ToggleAsync(int postId, string userId)
        {
            var existing = await _appDbContext.Likes.FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

            if (existing != null)
            {
                _appDbContext.Likes.Remove(existing);
            }
            else
            {
                var exists = await _appDbContext.Posts.AnyAsync(p => p.Id == postId);
                if (!exists)
                    return false;
                _appDbContext.Likes.Add(new Entities.Like
                {
                    PostId = postId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _appDbContext.SaveChangesAsync();
            return true;
        }


        // Removes like (idempotent)
        public async Task<bool> RemoveAsync(int postId, string userId)
        {
            var like = await _appDbContext.Likes.FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);
            if (like != null)
            {
                _appDbContext.Likes.Remove(like);
                await _appDbContext.SaveChangesAsync();
            }
            return true;
        }
    }
}
