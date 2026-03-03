using System.Collections.Generic;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IFeatureFlagsService
    {
        Task<bool> GetFlagAsync(string key, bool defaultValue);
        Task<Dictionary<string, string>> GetFlagsAsync();
        Task SetFlagAsync(string key, string value, string actorUserId);
    }
}
