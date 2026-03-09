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
    public class FollowService : IFollowService
    {
        private readonly AppDbContext _dbContext;

        public FollowService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task FollowAsync(string followerUserId, string followedUserId)
        {
            if (string.Equals(followerUserId, followedUserId, StringComparison.Ordinal))
            {
                throw new DomainValidationException("Invalid follow request.", new Dictionary<string, string[]>
                {
                    ["userId"] = new[] { "You cannot follow yourself." }
                });
            }

            var targetExists = await _dbContext.Users.AnyAsync(u => u.Id == followedUserId);
            if (!targetExists)
            {
                throw new NotFoundException("User not found.");
            }

            var alreadyFollowing = await _dbContext.Follows.AnyAsync(f =>
                f.FollowerUserId == followerUserId && f.FollowedUserId == followedUserId);

            if (alreadyFollowing)
            {
                throw new ConflictException("You already follow this user.");
            }

            _dbContext.Follows.Add(new Follow
            {
                FollowerUserId = followerUserId,
                FollowedUserId = followedUserId,
                CreatedUtc = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();
        }

        public async Task UnfollowAsync(string followerUserId, string followedUserId)
        {
            var follow = await _dbContext.Follows.FirstOrDefaultAsync(f =>
                f.FollowerUserId == followerUserId && f.FollowedUserId == followedUserId);

            if (follow == null)
            {
                throw new NotFoundException("Follow relationship not found.");
            }

            _dbContext.Follows.Remove(follow);
            await _dbContext.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<UserListItemDto>> GetFollowersAsync(string userId)
        {
            return await _dbContext.Follows
                .AsNoTracking()
                .Where(f => f.FollowedUserId == userId)
                .OrderByDescending(f => f.CreatedUtc)
                .Select(f => new UserListItemDto
                {
                    UserId = f.FollowerUser.Id,
                    UserName = f.FollowerUser.UserName ?? string.Empty,
                    DisplayName = f.FollowerUser.Profile != null && f.FollowerUser.Profile.DisplayName != null
                        ? f.FollowerUser.Profile.DisplayName
                        : (f.FollowerUser.UserName ?? string.Empty),
                    AvatarUrl = f.FollowerUser.Profile != null ? f.FollowerUser.Profile.AvatarUrl : f.FollowerUser.AvatarUrl
                })
                .ToListAsync();
        }

        public async Task<IReadOnlyList<UserListItemDto>> GetFollowingAsync(string userId)
        {
            return await _dbContext.Follows
                .AsNoTracking()
                .Where(f => f.FollowerUserId == userId)
                .OrderByDescending(f => f.CreatedUtc)
                .Select(f => new UserListItemDto
                {
                    UserId = f.FollowedUser.Id,
                    UserName = f.FollowedUser.UserName ?? string.Empty,
                    DisplayName = f.FollowedUser.Profile != null && f.FollowedUser.Profile.DisplayName != null
                        ? f.FollowedUser.Profile.DisplayName
                        : (f.FollowedUser.UserName ?? string.Empty),
                    AvatarUrl = f.FollowedUser.Profile != null ? f.FollowedUser.Profile.AvatarUrl : f.FollowedUser.AvatarUrl
                })
                .ToListAsync();
        }
    }
}
