using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IAuditLogService
    {
        Task AppendAsync(string actorUserId, string action, string targetType, string targetId, string? reason, string? oldValueJson, string? newValueJson);
    }
}
