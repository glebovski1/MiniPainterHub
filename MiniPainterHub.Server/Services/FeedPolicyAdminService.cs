using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public class FeedPolicyAdminService : IFeedPolicyAdminService
    {
        private readonly AppDbContext _db;
        private readonly IAuditLogService _audit;

        public FeedPolicyAdminService(AppDbContext db, IAuditLogService audit)
        {
            _db = db;
            _audit = audit;
        }

        public async Task<List<FeedPolicyDto>> GetAllAsync()
        {
            var items = await _db.FeedPolicies.AsNoTracking()
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => x.Name)
                .ToListAsync();
            return items.Select(Map).ToList();
        }

        public async Task<FeedPolicyDto> CreateAsync(string actorUserId, UpsertFeedPolicyDto dto)
        {
            var item = MapToEntity(new FeedPolicy { UpdatedAt = DateTime.UtcNow }, dto);
            _db.FeedPolicies.Add(item);
            await _db.SaveChangesAsync();
            await _audit.AppendAsync(actorUserId, "CreateFeedPolicy", "FeedPolicy", item.Id.ToString(), null, null, JsonSerializer.Serialize(Map(item)));
            return Map(item);
        }

        public async Task<FeedPolicyDto> UpdateAsync(string actorUserId, int id, UpsertFeedPolicyDto dto)
        {
            var item = await _db.FeedPolicies.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("Feed policy not found.");
            var oldValue = JsonSerializer.Serialize(Map(item));
            MapToEntity(item, dto);
            item.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _audit.AppendAsync(actorUserId, "UpdateFeedPolicy", "FeedPolicy", id.ToString(), null, oldValue, JsonSerializer.Serialize(Map(item)));
            return Map(item);
        }

        public async Task ActivateAsync(string actorUserId, int id)
        {
            var target = await _db.FeedPolicies.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("Feed policy not found.");

            var all = await _db.FeedPolicies.ToListAsync();
            foreach (var p in all)
            {
                p.IsActive = p.Id == id;
                p.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            await _audit.AppendAsync(actorUserId, "ActivateFeedPolicy", "FeedPolicy", target.Id.ToString(), null, null, JsonSerializer.Serialize(Map(target)));
        }

        private static FeedPolicy MapToEntity(FeedPolicy entity, UpsertFeedPolicyDto dto)
        {
            entity.Name = dto.Name;
            entity.WRecency = dto.WRecency;
            entity.WLikes = dto.WLikes;
            entity.WComments = dto.WComments;
            entity.WReportsPenalty = dto.WReportsPenalty;
            entity.HalfLifeHours = dto.HalfLifeHours;
            entity.DiversityByAuthor = dto.DiversityByAuthor;
            entity.MaxPerAuthorPerPage = dto.MaxPerAuthorPerPage;
            entity.ExcludeNSFW = dto.ExcludeNSFW;
            return entity;
        }

        private static FeedPolicyDto Map(FeedPolicy entity) => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            WRecency = entity.WRecency,
            WLikes = entity.WLikes,
            WComments = entity.WComments,
            WReportsPenalty = entity.WReportsPenalty,
            HalfLifeHours = entity.HalfLifeHours,
            DiversityByAuthor = entity.DiversityByAuthor,
            MaxPerAuthorPerPage = entity.MaxPerAuthorPerPage,
            ExcludeNSFW = entity.ExcludeNSFW,
            IsActive = entity.IsActive
        };
    }
}
