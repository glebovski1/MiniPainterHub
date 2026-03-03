using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public class UserModerationService : IUserModerationService
    {
        private readonly AppDbContext _db;
        private readonly IAuditLogService _audit;

        public UserModerationService(AppDbContext db, IAuditLogService audit)
        {
            _db = db;
            _audit = audit;
        }

        public async Task<UserRestrictionDto> GetOrDefaultAsync(string userId)
        {
            var item = await _db.UserRestrictions.FirstOrDefaultAsync(x => x.UserId == userId);
            if (item is null)
            {
                return new UserRestrictionDto { UserId = userId, CanPost = true, CanComment = true, CanPostImages = true };
            }

            if (item.Until.HasValue && item.Until.Value <= DateTime.UtcNow)
            {
                item.IsSuspended = false;
                item.CanPost = true;
                item.CanComment = true;
                item.CanPostImages = true;
                item.Reason = null;
                item.Until = null;
                await _db.SaveChangesAsync();
            }

            return Map(item);
        }

        public async Task<UserRestrictionDto> RestrictAsync(string actorUserId, string userId, SetUserRestrictionDto dto)
        {
            var item = await Ensure(userId);
            var oldValue = JsonSerializer.Serialize(Map(item));
            item.CanPost = dto.CanPost;
            item.CanComment = dto.CanComment;
            item.CanPostImages = dto.CanPostImages;
            item.Reason = dto.Reason;
            item.Until = dto.Until;
            await _db.SaveChangesAsync();
            await _audit.AppendAsync(actorUserId, "RestrictUser", "User", userId, dto.Reason, oldValue, JsonSerializer.Serialize(Map(item)));
            return Map(item);
        }

        public async Task<UserRestrictionDto> LiftAsync(string actorUserId, string userId)
        {
            var item = await Ensure(userId);
            var oldValue = JsonSerializer.Serialize(Map(item));
            item.CanPost = true;
            item.CanComment = true;
            item.CanPostImages = true;
            item.Reason = null;
            item.Until = null;
            await _db.SaveChangesAsync();
            await _audit.AppendAsync(actorUserId, "LiftUserRestriction", "User", userId, null, oldValue, JsonSerializer.Serialize(Map(item)));
            return Map(item);
        }

        public async Task<UserRestrictionDto> SuspendAsync(string actorUserId, string userId, SetSuspensionDto dto)
        {
            var item = await Ensure(userId);
            var oldValue = JsonSerializer.Serialize(Map(item));
            item.IsSuspended = true;
            item.Reason = dto.Reason;
            item.Until = dto.Until;
            await _db.SaveChangesAsync();
            await _audit.AppendAsync(actorUserId, "SuspendUser", "User", userId, dto.Reason, oldValue, JsonSerializer.Serialize(Map(item)));
            return Map(item);
        }

        public async Task<UserRestrictionDto> UnsuspendAsync(string actorUserId, string userId)
        {
            var item = await Ensure(userId);
            var oldValue = JsonSerializer.Serialize(Map(item));
            item.IsSuspended = false;
            item.Reason = null;
            item.Until = null;
            await _db.SaveChangesAsync();
            await _audit.AppendAsync(actorUserId, "UnsuspendUser", "User", userId, null, oldValue, JsonSerializer.Serialize(Map(item)));
            return Map(item);
        }

        private async Task<UserRestriction> Ensure(string userId)
        {
            var item = await _db.UserRestrictions.FirstOrDefaultAsync(x => x.UserId == userId);
            if (item is not null)
            {
                return item;
            }

            item = new UserRestriction { UserId = userId, CanPost = true, CanComment = true, CanPostImages = true };
            _db.UserRestrictions.Add(item);
            await _db.SaveChangesAsync();
            return item;
        }

        private static UserRestrictionDto Map(UserRestriction item) => new()
        {
            UserId = item.UserId,
            IsSuspended = item.IsSuspended,
            CanPost = item.CanPost,
            CanComment = item.CanComment,
            CanPostImages = item.CanPostImages,
            Reason = item.Reason,
            Until = item.Until
        };
    }
}
