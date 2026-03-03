using MiniPainterHub.Common.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IFeedPolicyAdminService
    {
        Task<List<FeedPolicyDto>> GetAllAsync();
        Task<FeedPolicyDto> CreateAsync(string actorUserId, UpsertFeedPolicyDto dto);
        Task<FeedPolicyDto> UpdateAsync(string actorUserId, int id, UpsertFeedPolicyDto dto);
        Task ActivateAsync(string actorUserId, int id);
    }
}
