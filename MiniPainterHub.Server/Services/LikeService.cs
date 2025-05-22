using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Services.Interfaces;
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

        // Returns total likes for a post
        public async Task<int> GetCountAsync(int postId)
        {
            return await _appDbContext.Likes
                .AsNoTracking()
                .CountAsync(l => l.PostId == postId);
        }

        // Checks if a specific user has liked the post
        public async Task<bool> IsLikedAsync(int postId, string userId)
        {
            return await _appDbContext.Likes
                .AsNoTracking()
                .AnyAsync(l => l.PostId == postId && l.UserId == userId);
        }

        // Toggles a like: adds if not present, removes if already liked
        public async Task<bool> ToggleAsync(int postId, string userId)
        {
            var existing = await _appDbContext.Likes
                .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

            if (existing != null)
            {
                _appDbContext.Likes.Remove(existing);
            }
            else
            {
                // Ensure post exists before adding
                var exists = await _appDbContext.Posts.AnyAsync(p => p.Id == postId);
                if (!exists)
                    return false;

                _appDbContext.Likes.Add(new Entities.Like
                {
                    PostId = postId,
                    UserId = userId,
                    CreatedAt = System.DateTime.UtcNow
                });
            }

            await _appDbContext.SaveChangesAsync();
            return true;
        }

        // Explicit remove (unlike)
        public async Task<bool> RemoveAsync(int postId, string userId)
        {
            var like = await _appDbContext.Likes
                .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);
            if (like == null)
                return false;

            _appDbContext.Likes.Remove(like);
            await _appDbContext.SaveChangesAsync();
            return true;
        }
    }
}
