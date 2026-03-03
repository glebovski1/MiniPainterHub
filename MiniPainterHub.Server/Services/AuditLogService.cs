using System;
using System.Threading.Tasks;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Services.Interfaces;

namespace MiniPainterHub.Server.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly AppDbContext _db;

        public AuditLogService(AppDbContext db)
        {
            _db = db;
        }

        public async Task AppendAsync(string actorUserId, string action, string targetType, string targetId, string? reason, string? oldValueJson, string? newValueJson)
        {
            _db.ModerationActions.Add(new ModerationAction
            {
                ActorUserId = actorUserId,
                Action = action,
                TargetType = targetType,
                TargetId = targetId,
                Reason = reason,
                OldValueJson = oldValueJson,
                NewValueJson = newValueJson,
                Timestamp = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
    }
}
