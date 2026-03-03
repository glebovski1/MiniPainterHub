using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IModerationService
    {
        Task HideAsync(string actorUserId, string type, int id, string? reason);
        Task UnhideAsync(string actorUserId, string type, int id, string? reason);
        Task SoftDeleteAsync(string actorUserId, string type, int id, string? reason);
        Task HardDeleteAsync(string actorUserId, string type, int id, string? reason);
    }
}
