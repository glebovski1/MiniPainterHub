using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public class ModerationService : IModerationService
    {
        private readonly AppDbContext _db;
        private readonly IAuditLogService _audit;

        public ModerationService(AppDbContext db, IAuditLogService audit)
        {
            _db = db;
            _audit = audit;
        }

        public Task HideAsync(string actorUserId, string type, int id, string? reason)
            => UpdateStatus(actorUserId, type, id, ContentStatus.Hidden, reason, false);

        public Task UnhideAsync(string actorUserId, string type, int id, string? reason)
            => UpdateStatus(actorUserId, type, id, ContentStatus.Active, reason, false);

        public Task SoftDeleteAsync(string actorUserId, string type, int id, string? reason)
            => UpdateStatus(actorUserId, type, id, ContentStatus.SoftDeleted, reason, true);

        public async Task HardDeleteAsync(string actorUserId, string type, int id, string? reason)
        {
            switch (type.ToLowerInvariant())
            {
                case "posts":
                case "post":
                    var post = await _db.Posts.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Content not found.");
                    _db.Posts.Remove(post);
                    break;
                case "comments":
                case "comment":
                    var comment = await _db.Comments.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Content not found.");
                    _db.Comments.Remove(comment);
                    break;
                case "images":
                case "image":
                    var image = await _db.PostImages.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Content not found.");
                    _db.PostImages.Remove(image);
                    break;
                default:
                    throw new DomainValidationException("Unsupported content type.");
            }

            await _db.SaveChangesAsync();
            await _audit.AppendAsync(actorUserId, "HardDelete", type, id.ToString(), reason, null, null);
        }

        private async Task UpdateStatus(string actorUserId, string type, int id, ContentStatus status, string? reason, bool setDeletedAt)
        {
            object oldObj;
            switch (type.ToLowerInvariant())
            {
                case "posts":
                case "post":
                    var post = await _db.Posts.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Content not found.");
                    oldObj = new { post.Status, post.ModerationNote, post.ModeratedAt, post.ModeratedByUserId, post.DeletedAt };
                    post.Status = status;
                    post.ModerationNote = reason;
                    post.ModeratedAt = DateTime.UtcNow;
                    post.ModeratedByUserId = actorUserId;
                    post.DeletedAt = setDeletedAt ? DateTime.UtcNow : null;
                    break;
                case "comments":
                case "comment":
                    var comment = await _db.Comments.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Content not found.");
                    oldObj = new { comment.Status, comment.ModerationNote, comment.ModeratedAt, comment.ModeratedByUserId, comment.DeletedAt };
                    comment.Status = status;
                    comment.ModerationNote = reason;
                    comment.ModeratedAt = DateTime.UtcNow;
                    comment.ModeratedByUserId = actorUserId;
                    comment.DeletedAt = setDeletedAt ? DateTime.UtcNow : null;
                    break;
                case "images":
                case "image":
                    var image = await _db.PostImages.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Content not found.");
                    oldObj = new { image.Status, image.ModerationNote, image.ModeratedAt, image.ModeratedByUserId, image.DeletedAt };
                    image.Status = status;
                    image.ModerationNote = reason;
                    image.ModeratedAt = DateTime.UtcNow;
                    image.ModeratedByUserId = actorUserId;
                    image.DeletedAt = setDeletedAt ? DateTime.UtcNow : null;
                    break;
                default:
                    throw new DomainValidationException("Unsupported content type.");
            }

            await _db.SaveChangesAsync();
            await _audit.AppendAsync(actorUserId, status.ToString(), type, id.ToString(), reason, JsonSerializer.Serialize(oldObj), JsonSerializer.Serialize(new { Status = status, Reason = reason }));
        }
    }
}
