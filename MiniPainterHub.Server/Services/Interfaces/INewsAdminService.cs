using MiniPainterHub.Common.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface INewsAdminService
    {
        Task<List<NewsItemDto>> GetAllAsync();
        Task<NewsItemDto> CreateAsync(string actorUserId, UpsertNewsItemDto dto);
        Task<NewsItemDto> UpdateAsync(string actorUserId, int id, UpsertNewsItemDto dto);
        Task HideAsync(string actorUserId, int id, string? reason);
        Task UnhideAsync(string actorUserId, int id, string? reason);
        Task DeleteAsync(string actorUserId, int id, string? reason);
    }
}
