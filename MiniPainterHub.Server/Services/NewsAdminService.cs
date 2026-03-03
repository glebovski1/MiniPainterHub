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
    public class NewsAdminService : INewsAdminService
    {
        private readonly AppDbContext _db;
        private readonly IAuditLogService _audit;

        public NewsAdminService(AppDbContext db, IAuditLogService audit)
        {
            _db = db;
            _audit = audit;
        }

        public async Task<List<NewsItemDto>> GetAllAsync()
        {
            var items = await _db.NewsItems.IgnoreQueryFilters().AsNoTracking()
                .OrderByDescending(x => x.PublishAt)
                .ToListAsync();
            return items.Select(Map).ToList();
        }

        public async Task<NewsItemDto> CreateAsync(string actorUserId, UpsertNewsItemDto dto)
        {
            var item = new NewsItem
            {
                Title = dto.Title,
                BodyMarkdown = dto.BodyMarkdown,
                PublishAt = dto.PublishAt,
                ExpiresAt = dto.ExpiresAt,
                IsPinned = dto.IsPinned,
                PinPriority = dto.PinPriority,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.NewsItems.Add(item);
            await _db.SaveChangesAsync();
            await _audit.AppendAsync(actorUserId, "CreateNews", "News", item.Id.ToString(), null, null, JsonSerializer.Serialize(Map(item)));
            return Map(item);
        }

        public async Task<NewsItemDto> UpdateAsync(string actorUserId, int id, UpsertNewsItemDto dto)
        {
            var item = await _db.NewsItems.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("News item not found.");
            var oldValue = JsonSerializer.Serialize(Map(item));

            item.Title = dto.Title;
            item.BodyMarkdown = dto.BodyMarkdown;
            item.PublishAt = dto.PublishAt;
            item.ExpiresAt = dto.ExpiresAt;
            item.IsPinned = dto.IsPinned;
            item.PinPriority = dto.PinPriority;
            item.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await _audit.AppendAsync(actorUserId, "UpdateNews", "News", id.ToString(), null, oldValue, JsonSerializer.Serialize(Map(item)));
            return Map(item);
        }

        public Task HideAsync(string actorUserId, int id, string? reason) => SetStatusAsync(actorUserId, id, ContentStatus.Hidden, reason, "HideNews");
        public Task UnhideAsync(string actorUserId, int id, string? reason) => SetStatusAsync(actorUserId, id, ContentStatus.Active, reason, "UnhideNews");

        public async Task DeleteAsync(string actorUserId, int id, string? reason)
        {
            var item = await _db.NewsItems.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("News item not found.");
            var oldValue = JsonSerializer.Serialize(Map(item));
            _db.NewsItems.Remove(item);
            await _db.SaveChangesAsync();
            await _audit.AppendAsync(actorUserId, "DeleteNews", "News", id.ToString(), reason, oldValue, null);
        }

        private async Task SetStatusAsync(string actorUserId, int id, ContentStatus status, string? reason, string action)
        {
            var item = await _db.NewsItems.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("News item not found.");
            var oldValue = JsonSerializer.Serialize(Map(item));
            item.Status = status;
            item.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _audit.AppendAsync(actorUserId, action, "News", id.ToString(), reason, oldValue, JsonSerializer.Serialize(Map(item)));
        }

        private static NewsItemDto Map(NewsItem item) => new()
        {
            Id = item.Id,
            Title = item.Title,
            BodyMarkdown = item.BodyMarkdown,
            PublishAt = item.PublishAt,
            ExpiresAt = item.ExpiresAt,
            IsPinned = item.IsPinned,
            PinPriority = item.PinPriority,
            Status = item.Status.ToString()
        };
    }
}
